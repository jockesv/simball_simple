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
                // Use ball interception instead of chasing current position
                var interceptPosition = GeometryHelper.CalculateInterceptPosition(playerStatus, ballStatus);
                
                return new PlayerInstructions
                {
                    MoveToX = interceptPosition.X,
                    MoveToY = interceptPosition.Y,
                    MoveVelocity = 100,
                    BallTargetX = 12000,
                    BallTargetY = 4500,
                    BallVelocity = Math.Min(100, 60 + Math.Abs(playerStatus.Vx) / 10)
                };
            }

            // If player has the ball (very close), make a decision
            if (distanceToBall < 150)
            {
                // Check for passing opportunities first
                var otherAttackerNumber = playerNumber == 3 ? 4 : 3;
                var otherAttacker = PlayerHelper.GetPlayerStatus(gameStatus, otherAttackerNumber);
                
                // Pass if teammate is in better position AND we can pass in that direction
                if (otherAttacker.X > playerStatus.X + 800 && otherAttacker.X > 7000)
                {
                    if (GeometryHelper.CanPlayerShootInDirection(playerStatus, otherAttacker.X, otherAttacker.Y))
                    {
                        return new PlayerInstructions
                        {
                            BallTargetX = otherAttacker.X + 200, // Lead the pass slightly
                            BallTargetY = otherAttacker.Y,
                            BallVelocity = 75
                        };
                    }
                }
                
                // If in good shooting position, shoot (with direction limitations)
                if (playerStatus.X > 8000)
                {
                    var goalTarget = GeometryHelper.GetBestShootableTarget(playerStatus, 12000, 4500);
                    var shotVelocity = Math.Min(100, 50 + (Math.Abs(playerStatus.Vx) + Math.Abs(playerStatus.Vy)) / 20);
                    
                    return new PlayerInstructions
                    {
                        BallTargetX = goalTarget.X,
                        BallTargetY = goalTarget.Y,
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
                // We have possession - position for pass or goal scoring
                var ballCarrier = ourClosestToBall;
                
                // If ball is in attacking third, position for goal scoring/tap-ins
                if (ballStatus.X > 8000)
                {
                    var goalAreaX = 11000 + (playerNumber - 3) * 500; // Spread around goal area
                    var goalAreaY = 4500 + (playerNumber == 3 ? -800 : 800); // One high, one low
                    
                    return new PlayerInstructions
                    {
                        MoveToX = goalAreaX,
                        MoveToY = goalAreaY,
                        MoveVelocity = 90
                    };
                }
                
                // Otherwise, position for receiving passes
                var passReceiveX = Math.Max(6000, ballStatus.X + 1500); // Ahead of ball
                var passReceiveY = ballStatus.Y + (playerNumber == 3 ? -1500 : 1500); // Spread vertically
                
                // Stay within field bounds
                passReceiveY = Math.Max(1000, Math.Min(passReceiveY, 8000));
                
                return new PlayerInstructions
                {
                    MoveToX = passReceiveX,
                    MoveToY = passReceiveY,
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

                // Closest defender goes for the ball using interception
                if ((playerNumber == 1 && distance1ToBall <= distance2ToBall) || 
                    (playerNumber == 2 && distance2ToBall < distance1ToBall))
                {
                    var interceptPosition = GeometryHelper.CalculateInterceptPosition(currentPlayer, ballStatus);
                    
                    return new PlayerInstructions
                    {
                        MoveToX = interceptPosition.X,
                        MoveToY = interceptPosition.Y,
                        MoveVelocity = 100,
                        BallTargetX = FindBestPassTarget(gameStatus, playerNumber),
                        BallTargetY = FindBestPassTargetY(gameStatus, playerNumber),
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
                    // Cover the goal - position to block shots more effectively
                    var opponentX = opponentClosestToBall.X;
                    var opponentY = opponentClosestToBall.Y;
                    
                    // Calculate the line from opponent to goal center
                    var lineDirectionX = goalX - opponentX;
                    var lineDirectionY = goalY - opponentY;
                    var lineLength = Math.Sqrt(lineDirectionX * lineDirectionX + lineDirectionY * lineDirectionY);
                    
                    // Normalize the direction vector
                    var normalizedX = lineDirectionX / lineLength;
                    var normalizedY = lineDirectionY / lineLength;
                    
                    // Position closer to goal but with better angle coverage
                    var blockDistance = Math.Min(1500, lineLength * 0.7); // 70% towards goal
                    var blockX = opponentX + normalizedX * blockDistance;
                    var blockY = opponentY + normalizedY * blockDistance;
                    
                    // Add slight offset to cover more angle
                    // If opponent is to the right of goal, defender goes slightly left and vice versa
                    var lateralOffset = 0;
                    if (opponentY < goalY - 500) // Opponent is above goal center
                    {
                        lateralOffset = 300; // Move down to cover that side
                    }
                    else if (opponentY > goalY + 500) // Opponent is below goal center
                    {
                        lateralOffset = -300; // Move up to cover that side
                    }
                    
                    blockY += lateralOffset;
                    
                    // Ensure we stay within reasonable bounds and don't go too far back
                    blockX = Math.Max(400, Math.Min(blockX, 2500));
                    blockY = Math.Max(1500, Math.Min(blockY, 7500));
                    
                    return new PlayerInstructions
                    {
                        MoveToX = (int)blockX,
                        MoveToY = (int)blockY,
                        MoveVelocity = 95
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

        private int FindBestPassTarget(GameStatusExtended gameStatus, int currentPlayer)
        {
            var currentPlayerStatus = PlayerHelper.GetPlayerStatus(gameStatus, currentPlayer);
            var bestX = 0;
            var bestTeammateFound = false;
            
            for (int i = 1; i <= 4; i++)
            {
                if (i == currentPlayer) continue;
                
                var teammate = PlayerHelper.GetPlayerStatus(gameStatus, i);
                
                // Endast överväg lagkamrater som vi faktiskt kan passa till
                if (GeometryHelper.CanPlayerShootInDirection(currentPlayerStatus, teammate.X, teammate.Y))
                {
                    if (teammate.X > bestX)
                    {
                        bestX = teammate.X;
                        bestTeammateFound = true;
                    }
                }
            }
            
            // Om ingen passbar lagkamrat hittades, hitta bästa riktning att spela framåt
            if (!bestTeammateFound)
            {
                var forwardTarget = GeometryHelper.GetBestShootableTarget(currentPlayerStatus, 12000, 4500);
                return forwardTarget.X;
            }
            
            // Passa lite framför lagkamraten
            return bestX + 500;
        }

        private int FindBestPassTargetY(GameStatusExtended gameStatus, int currentPlayer)
        {
            var currentPlayerStatus = PlayerHelper.GetPlayerStatus(gameStatus, currentPlayer);
            var bestX = 0;
            var bestY = 4500;
            var bestTeammateFound = false;
            
            for (int i = 1; i <= 4; i++)
            {
                if (i == currentPlayer) continue;
                
                var teammate = PlayerHelper.GetPlayerStatus(gameStatus, i);
                
                // Endast överväg lagkamrater som vi faktiskt kan passa till
                if (GeometryHelper.CanPlayerShootInDirection(currentPlayerStatus, teammate.X, teammate.Y))
                {
                    if (teammate.X > bestX)
                    {
                        bestX = teammate.X;
                        bestY = teammate.Y;
                        bestTeammateFound = true;
                    }
                }
            }
            
            // Om ingen passbar lagkamrat hittades, sikta mot centrum
            if (!bestTeammateFound)
            {
                var forwardTarget = GeometryHelper.GetBestShootableTarget(currentPlayerStatus, 12000, 4500);
                return forwardTarget.Y;
            }
            
            return bestY;
        }
    }
}
