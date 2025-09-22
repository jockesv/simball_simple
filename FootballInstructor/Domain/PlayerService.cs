using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain
{
    /// <summary>
    /// TODO Change this logic
    /// Here all players chase the ball and shoots at goal
    /// </summary>
    public class PlayerService : IPlayerService
    {
        /// <summary>
        /// Anropas när laget registreras och ger tillbaka namn och klädfärger
        /// </summary>
        /// <returns>Laginfo</returns>
        public TeamSetup Setup()
        {
            return new TeamSetup()
            {
                Name = "Api FK",
                PrimaryColor = new TeamColors()
                {
                    Jearsey = "0000FF",
                    Pants = "FFFF00",
                    Socks = "0000FF",
                },
                SecondaryColor = new TeamColors()
                {
                    Jearsey = "FFFF00",
                    Pants = "0000FF",
                    Socks = "FFFF00",
                }
            };
        }

        /// <summary>
        /// Anropas flera gånger per sekund (ca 4) och ger status för matchen och vill ha tillbaka instruktioner till spelarna.
        /// </summary>
        /// <param name="gameStatus"></param>
        /// <returns>Instruktioner till laget</returns>
        public virtual TeamInstructions Update(GameStatusExtended gameStatus)
        {
            var p1Instructions = GetPlayerInstructions(gameStatus, 1);
            var p2Instructions = GetPlayerInstructions(gameStatus, 2);
            var p3Instructions = GetPlayerInstructions(gameStatus, 3);
            var p4Instructions = GetPlayerInstructions(gameStatus, 4);

            return new TeamInstructions()
            {
                P1Instructions = p1Instructions,
                P2Instructions = p2Instructions,
                P3Instructions = p3Instructions,
                P4Instructions = p4Instructions,
            };
        }

        private PlayerInstructions GetPlayerInstructions(GameStatusExtended gameStatus, int playerNumber)
        {
            var playerRole = PlayerHelper.GetPlayerRole(playerNumber);

            return playerRole switch
            {
                PlayerRole.Attacker => GetAttackerInstructions(gameStatus, playerNumber),
                PlayerRole.Defender => GetDefenderInstructions(gameStatus, playerNumber),
                _ => new PlayerInstructions()
            };
        }

        private PlayerInstructions GetAttackerInstructions(GameStatusExtended gameStatus, int playerNumber)
        {
            var playerStatus = PlayerHelper.GetPlayerStatus(gameStatus, playerNumber);
            var ballStatus = gameStatus.BallStatus;
            var distanceToBall = GeometryHelper.Distance(playerStatus, ballStatus);

            // Check if we or opponents have ball possession
            var ourClosestToBall = GetOurClosestPlayerToBall(gameStatus);
            var opponentClosestToBall = PlayerHelper.GetClosestOpponentToBall(gameStatus);
            
            var ourDistanceToBall = GeometryHelper.Distance(ourClosestToBall, ballStatus);
            var opponentDistanceToBall = GeometryHelper.Distance(opponentClosestToBall, ballStatus);

            // If this attacker is closest to ball among our team, go for it
            var isClosestToEnemyPlayer = ourClosestToBall.X == playerStatus.X && ourClosestToBall.Y == playerStatus.Y;
            
            if (isClosestToEnemyPlayer && ourDistanceToBall <= opponentDistanceToBall + 150)
            {
                // Go for the ball
                return new PlayerInstructions
                {
                    MoveToX = ballStatus.X,
                    MoveToY = ballStatus.Y,
                    MoveVelocity = 100,
                    BallTargetX = 12000,
                    BallTargetY = 4500,
                    BallVelocity = Math.Min(100, 60 + Math.Abs(playerStatus.Vx) / 10)
                };
            }

            // If player has the ball (very close), make a decision
            if (distanceToBall < 150)
            {
                // If in good shooting position, shoot
                if (playerStatus.X > 8000)
                {
                    var shotVelocity = Math.Min(100, 50 + (Math.Abs(playerStatus.Vx) + Math.Abs(playerStatus.Vy)) / 20);
                    return new PlayerInstructions
                    {
                        BallTargetX = 12000,
                        BallTargetY = 4500,
                        BallVelocity = shotVelocity
                    };
                }

                // Otherwise, dribble towards goal
                return new PlayerInstructions
                {
                    MoveToX = Math.Min(11500, playerStatus.X + 500),
                    MoveToY = 4500,
                    MoveVelocity = 80
                };
            }

            // If we don't have the ball, get into good attacking position
            if (ourDistanceToBall < opponentDistanceToBall)
            {
                // We have possession, get into scoring position
                var targetX = Math.Max(7000, ballStatus.X + 1000);
                var targetY = playerNumber == 3 ? 3000 : 6000;
                
                return new PlayerInstructions
                {
                    MoveToX = targetX,
                    MoveToY = targetY,
                    MoveVelocity = 80
                };
            }
            else
            {
                // Opponents have ball, track back to help defense but stay forward
                var targetX = Math.Max(6000, ballStatus.X - 1000);
                var targetY = playerNumber == 3 ? 3500 : 5500;
                
                return new PlayerInstructions
                {
                    MoveToX = targetX,
                    MoveToY = targetY,
                    MoveVelocity = 70
                };
            }
        }

        private PlayerInstructions GetDefenderInstructions(GameStatusExtended gameStatus, int playerNumber)
        {
            var currentPlayer = PlayerHelper.GetPlayerStatus(gameStatus, playerNumber);
            var ballStatus = gameStatus.BallStatus;
            var goalX = 0;
            var goalY = 4500;

            // Check if we have the ball or if an opponent has it
            var ourClosestToBall = GetOurClosestPlayerToBall(gameStatus);
            var opponentClosestToBall = PlayerHelper.GetClosestOpponentToBall(gameStatus);
            
            var ourDistanceToBall = GeometryHelper.Distance(ourClosestToBall, ballStatus);
            var opponentDistanceToBall = GeometryHelper.Distance(opponentClosestToBall, ballStatus);

            // If we have possession or ball is neutral, one defender goes for ball
            if (ourDistanceToBall <= opponentDistanceToBall + 100) // We have or can get the ball
            {
                var defender1 = gameStatus.YourStatus.P1Status;
                var defender2 = gameStatus.YourStatus.P2Status;
                var distance1ToBall = GeometryHelper.Distance(defender1, ballStatus);
                var distance2ToBall = GeometryHelper.Distance(defender2, ballStatus);

                // Closest defender goes for the ball
                if ((playerNumber == 1 && distance1ToBall <= distance2ToBall) || 
                    (playerNumber == 2 && distance2ToBall < distance1ToBall))
                {
                    return new PlayerInstructions
                    {
                        MoveToX = ballStatus.X,
                        MoveToY = ballStatus.Y,
                        MoveVelocity = 100,
                        BallTargetX = 12000, // Clear towards opponent goal
                        BallTargetY = 4500,
                        BallVelocity = 80
                    };
                }
                else
                {
                    // Other defender takes defensive position
                    var defenseX = Math.Max(1000, ballStatus.X - 2000); // Stay behind the ball
                    var defenseY = playerNumber == 1 ? 3000 : 6000; // Cover different areas
                    
                    return new PlayerInstructions
                    {
                        MoveToX = defenseX,
                        MoveToY = defenseY,
                        MoveVelocity = 70
                    };
                }
            }
            else
            {
                // Opponent has the ball - implement proper defensive blocking
                var defender1 = gameStatus.YourStatus.P1Status;
                var defender2 = gameStatus.YourStatus.P2Status;
                
                // Determine roles: one pressures, one covers goal
                var distance1ToOpponent = GeometryHelper.Distance(defender1, opponentClosestToBall);
                var distance2ToOpponent = GeometryHelper.Distance(defender2, opponentClosestToBall);
                
                var shouldPressure = (playerNumber == 1 && distance1ToOpponent <= distance2ToOpponent) ||
                                   (playerNumber == 2 && distance2ToOpponent < distance1ToOpponent);
                
                if (shouldPressure)
                {
                    // Pressure the opponent with the ball, but stay between them and goal
                    var pressureX = opponentClosestToBall.X - 200; // Stay slightly back
                    var pressureY = opponentClosestToBall.Y;
                    
                    return new PlayerInstructions
                    {
                        MoveToX = Math.Max(500, pressureX), // Don't go too far forward
                        MoveToY = pressureY,
                        MoveVelocity = 100
                    };
                }
                else
                {
                    // Cover the goal - position on the line between opponent and goal center
                    var opponentX = opponentClosestToBall.X;
                    var opponentY = opponentClosestToBall.Y;
                    
                    // Calculate the line from opponent to goal
                    var lineDirectionX = goalX - opponentX;
                    var lineDirectionY = goalY - opponentY;
                    
                    // Position 60% of the way from opponent to goal (closer to goal)
                    var blockX = opponentX + lineDirectionX * 0.6;
                    var blockY = opponentY + lineDirectionY * 0.6;
                    
                    // Ensure we stay within reasonable bounds
                    blockX = Math.Max(200, Math.Min(blockX, 3000));
                    blockY = Math.Max(1000, Math.Min(blockY, 8000));
                    
                    return new PlayerInstructions
                    {
                        MoveToX = (int)blockX,
                        MoveToY = (int)blockY,
                        MoveVelocity = 90
                    };
                }
            }
        }

        private PlayerStatus GetOurClosestPlayerToBall(GameStatusExtended gameStatus)
        {
            return GeometryHelper.GetClosestPlayer(gameStatus.BallStatus,
                gameStatus.YourStatus.P1Status,
                gameStatus.YourStatus.P2Status,
                gameStatus.YourStatus.P3Status,
                gameStatus.YourStatus.P4Status);
        }
    }
}
