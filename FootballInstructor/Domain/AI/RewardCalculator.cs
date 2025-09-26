using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain.AI
{
    public class RewardCalculator
    {
        private GameStatusExtended? _previousState;
        private float _previousDistanceToBall = float.MaxValue;
        private int _previousBallX = 0;
        private Dictionary<int, (int X, int Y)> _previousPlayerPositions = new();
        private int? _lastYourScore;
        private int? _lastOpponentScore;
        private bool _pendingYourGoal;
        private bool _pendingOpponentGoal;

        public float CalculateReward(GameStatusExtended gameState, TeamInstructions action, GameStatusExtended? nextState = null)
        {
            // Minimalistic reward shaping: only goals and direct ball interaction
            float reward = 0;

            // Goals - detect via score deltas and fall back to transient flags
            var next = nextState ?? gameState;
            var latestYourScore = next.YourScore;
            var latestOpponentScore = next.OpponentScore;
            var previousYourScore = _lastYourScore ?? gameState.YourScore;
            var previousOpponentScore = _lastOpponentScore ?? gameState.OpponentScore;

            var yourScoreDelta = latestYourScore - previousYourScore;
            var opponentScoreDelta = latestOpponentScore - previousOpponentScore;

            var youScoredFlag = next.YouScored || gameState.YouScored;
            var opponentScoredFlag = next.OpponentScored || gameState.OpponentScored;

            if (yourScoreDelta > 0)
            {
                if (_pendingYourGoal)
                {
                    // Already rewarded via flag, add remaining delta beyond the first goal if needed
                    reward += 100f * Math.Max(0, yourScoreDelta - 1);
                    _pendingYourGoal = false;
                }
                else
                {
                    reward += 100f * yourScoreDelta;
                }
            }
            else if (yourScoreDelta == 0 && youScoredFlag && !_pendingYourGoal)
            {
                // Flag fired before scoreboard updated - reward once and wait for score to catch up
                reward += 100f;
                _pendingYourGoal = true;
            }

            if (opponentScoreDelta > 0)
            {
                if (_pendingOpponentGoal)
                {
                    reward -= 100f * Math.Max(0, opponentScoreDelta - 1);
                    _pendingOpponentGoal = false;
                }
                else
                {
                    reward -= 100f * opponentScoreDelta;
                }
            }
            else if (opponentScoreDelta == 0 && opponentScoredFlag && !_pendingOpponentGoal)
            {
                reward -= 100f;
                _pendingOpponentGoal = true;
            }

            if (!youScoredFlag)
                _pendingYourGoal = false;
            if (!opponentScoredFlag)
                _pendingOpponentGoal = false;

            _lastYourScore = latestYourScore;
            _lastOpponentScore = latestOpponentScore;

            // Direct ball interaction
            // - Touch: any of our players within small radius of the ball
            // - Kick: issuing a BallVelocity while near the ball
            var players = new[]
            {
                (Status: gameState.YourStatus.P1Status, Instr: action.P1Instructions),
                (Status: gameState.YourStatus.P2Status, Instr: action.P2Instructions),
                (Status: gameState.YourStatus.P3Status, Instr: action.P3Instructions),
                (Status: gameState.YourStatus.P4Status, Instr: action.P4Instructions)
            };

            const float touchRadius = 300f;   // ~3 meters
            const float kickRadius = 350f;    // slightly larger to account for timing

            foreach (var p in players)
            {
                var dist = GeometryHelper.Distance(p.Status, gameState.BallStatus);

                // // Touch/dribble: being right next to the ball
                // if (dist < touchRadius)
                // {
                //     reward += 1.0f;
                // }

                // Kick/pass/shot: commanding ball velocity while near the ball
                // if (p.Instr.BallVelocity > 0 && dist < kickRadius)
                // {
                //     reward += 2.0f;
                // }
            }

            // Store previous state for potential future use
            _previousState = gameState;

            // Debug: Log rewards to ensure we're only getting goal rewards
            if (reward != 0)
            {
                Console.WriteLine($"[DEBUG] Reward given: {reward} (YouScored: {gameState.YouScored}, OpponentScored: {gameState.OpponentScored})");
            }

            // Keep a modest clamp
            return Math.Clamp(reward, -100f, 100f);
        }

        private float CalculateBallPossessionReward(GameStatusExtended gameState)
        {
            var ourClosestToBall = GeometryHelper.GetClosestPlayer(gameState.BallStatus,
                gameState.YourStatus.P1Status,
                gameState.YourStatus.P2Status,
                gameState.YourStatus.P3Status,
                gameState.YourStatus.P4Status);

            var opponentClosestToBall = PlayerHelper.GetClosestOpponentToBall(gameState);

            var ourDistance = GeometryHelper.Distance(ourClosestToBall, gameState.BallStatus);
            var opponentDistance = GeometryHelper.Distance(opponentClosestToBall, gameState.BallStatus);

            float reward = 0;

            // Check if our closest player was already hiding in corner before ball arrived
            bool wasHidingInCorner = IsPlayerInCorner(ourClosestToBall.X, ourClosestToBall.Y) && 
                                   WasPlayerInCornerPreviously(ourClosestToBall);

            // Strong reward for being close to ball - BUT NOT if player was corner-hiding!
            if (ourDistance < opponentDistance && !wasHidingInCorner)
            {
                if (ourDistance < 150) // Very close to ball
                    reward += 3.0f;
                else if (ourDistance < 300) // Close to ball
                    reward += 2.0f;
                else if (ourDistance < 600) // Moderately close
                    reward += 1.0f;
                else // Closer than opponent but not too close
                    reward += 0.5f;
            }
            else if (wasHidingInCorner && ourDistance < opponentDistance)
            {
                // Severe penalty for getting ball while corner hiding - discourages this behavior!
                reward -= 5.0f;
            }

            // EXTREME penalty for being very far from ball (prevents corner hiding!)
            if (ourDistance > 4000) // Very far from ball
                reward -= 10.0f; // MASSIVELY increased penalty
            else if (ourDistance > 2000) // Far from ball
                reward -= 6.0f; // Significantly increased penalty

            // Check EACH individual player for corner hiding - severe individual penalties
            var allPlayerDistances = new[]
            {
                GeometryHelper.Distance(gameState.YourStatus.P1Status, gameState.BallStatus),
                GeometryHelper.Distance(gameState.YourStatus.P2Status, gameState.BallStatus),
                GeometryHelper.Distance(gameState.YourStatus.P3Status, gameState.BallStatus),
                GeometryHelper.Distance(gameState.YourStatus.P4Status, gameState.BallStatus)
            };

            var currentPlayers = new[]
            {
                gameState.YourStatus.P1Status,
                gameState.YourStatus.P2Status,
                gameState.YourStatus.P3Status,
                gameState.YourStatus.P4Status
            };

            // SEVERE penalty for EACH player in corners
            for (int i = 0; i < currentPlayers.Length; i++)
            {
                if (IsPlayerInCorner(currentPlayers[i].X, currentPlayers[i].Y))
                {
                    reward -= 15.0f; // MASSIVE penalty per corner player
                }
                
                // Extra penalty for being extremely far from ball
                if (allPlayerDistances[i] > 6000)
                    reward -= 8.0f; // Much higher penalty for extreme distance
                else if (allPlayerDistances[i] > 4000)
                    reward -= 5.0f; // High penalty for far distance
            }

            // Additional penalty if ALL players are far from ball
            if (allPlayerDistances.All(d => d > 1500)) // All players far from ball
                reward -= 20.0f; // MASSIVE penalty for complete passive play

            // Count players in corners and apply escalating penalties
            var playersInCorners = currentPlayers.Count(p => IsPlayerInCorner(p.X, p.Y));
            if (playersInCorners > 0)
            {
                reward -= playersInCorners * playersInCorners * 10.0f; // Exponential penalty: 1 player = -10, 2 players = -40, 3 players = -90, 4 players = -160
            }

            return reward;
        }

        private float CalculatePositionalReward(GameStatusExtended gameState)
        {
            float reward = 0;

            // Reward defenders for staying back when opponent has ball
            var ourClosestToBall = GeometryHelper.GetClosestPlayer(gameState.BallStatus,
                gameState.YourStatus.P1Status,
                gameState.YourStatus.P2Status,
                gameState.YourStatus.P3Status,
                gameState.YourStatus.P4Status);

            var opponentClosestToBall = PlayerHelper.GetClosestOpponentToBall(gameState);
            var ourDistance = GeometryHelper.Distance(ourClosestToBall, gameState.BallStatus);
            var opponentDistance = GeometryHelper.Distance(opponentClosestToBall, gameState.BallStatus);

            if (opponentDistance < ourDistance) // Opponent has ball
            {
                // Reward defenders for good defensive positioning
                var defender1Distance = GeometryHelper.Distance(gameState.YourStatus.P1Status.X, gameState.YourStatus.P1Status.Y, 0, 4500);
                var defender2Distance = GeometryHelper.Distance(gameState.YourStatus.P2Status.X, gameState.YourStatus.P2Status.Y, 0, 4500);

                if (defender1Distance < 3000) reward += 0.2f;
                if (defender2Distance < 3000) reward += 0.2f;
            }
            else // We have ball
            {
                // Reward attackers for moving forward
                if (gameState.YourStatus.P3Status.X > 6000) reward += 0.3f;
                if (gameState.YourStatus.P4Status.X > 6000) reward += 0.3f;

                // Extra reward for being in attacking third
                if (gameState.YourStatus.P3Status.X > 8000) reward += 0.5f;
                if (gameState.YourStatus.P4Status.X > 8000) reward += 0.5f;
            }

            return reward;
        }

        private float CalculateBallAdvancementReward(GameStatusExtended gameState)
        {
            // Reward for advancing ball towards opponent goal
            var ballAdvancement = gameState.BallStatus.X - _previousBallX;
            _previousBallX = gameState.BallStatus.X;

            if (ballAdvancement > 0) // Ball moved towards opponent goal
            {
                return Math.Min(ballAdvancement / 1000f, 1.0f); // Max 1.0 reward
            }
            else if (ballAdvancement < -500) // Ball moved significantly backwards
            {
                return Math.Max(ballAdvancement / 1000f, -0.5f); // Max -0.5 penalty
            }

            return 0;
        }

        private float CalculateDefensiveReward(GameStatusExtended gameState)
        {
            float reward = 0;

            // Reward for keeping opponents away from our goal
            var opponentClosestToOurGoal = GeometryHelper.GetClosestPlayer(
                new BallStatus { X = 0, Y = 4500 }, // Our goal position
                gameState.OpponentStatus.P1Status,
                gameState.OpponentStatus.P2Status,
                gameState.OpponentStatus.P3Status,
                gameState.OpponentStatus.P4Status);

            var distanceToOurGoal = GeometryHelper.Distance(opponentClosestToOurGoal.X, opponentClosestToOurGoal.Y, 0, 4500);

            if (distanceToOurGoal > 5000) // Opponents far from our goal
                reward += 0.5f;
            else if (distanceToOurGoal < 2000) // Opponents very close to our goal
                reward -= 1.0f;

            return reward;
        }

        private float CalculateActionPenalties(GameStatusExtended gameState, TeamInstructions action)
        {
            float penalty = 0;

            // Check for corner hiding behavior - severe penalty!
            var players = new[] 
            {
                new { X = action.P1Instructions.MoveToX, Y = action.P1Instructions.MoveToY },
                new { X = action.P2Instructions.MoveToX, Y = action.P2Instructions.MoveToY },
                new { X = action.P3Instructions.MoveToX, Y = action.P3Instructions.MoveToY },
                new { X = action.P4Instructions.MoveToX, Y = action.P4Instructions.MoveToY }
            };

            // EXTREME penalty for corner hiding - make it absolutely unprofitable
            foreach (var player in players)
            {
                // Top corners - MASSIVE penalties
                if ((player.X < 1000 && player.Y < 1000) || (player.X > 11000 && player.Y < 1000))
                    penalty -= 20.0f; // Increased from 3.0f to 20.0f
                // Bottom corners - MASSIVE penalties
                if ((player.X < 1000 && player.Y > 8000) || (player.X > 11000 && player.Y > 8000))
                    penalty -= 20.0f; // Increased from 3.0f to 20.0f
                
                // Wider corner detection - catch players trying to "almost hide"
                if ((player.X < 2000 && player.Y < 2000) || (player.X > 10000 && player.Y < 2000) ||
                    (player.X < 2000 && player.Y > 7000) || (player.X > 10000 && player.Y > 7000))
                    penalty -= 10.0f; // Penalty for "near-corner" positions
                
                // Edge hiding (along boundaries without purpose)
                if (player.X < 500 || player.X > 11500 || player.Y < 500 || player.Y > 8500)
                    penalty -= 5.0f; // Increased from 1.0f to 5.0f
            }

            // Count players moving to corners and add massive escalating penalty
            var playersMovingToCorners = players.Count(p => IsPlayerInCorner(p.X, p.Y));
            if (playersMovingToCorners > 0)
            {
                penalty -= playersMovingToCorners * playersMovingToCorners * 15.0f; // Exponential: 1=-15, 2=-60, 3=-135, 4=-240
            }

            // Much lighter formation penalties - only for extreme cases
            for (int i = 0; i < players.Length; i++)
            {
                for (int j = i + 1; j < players.Length; j++)
                {
                    var distance = GeometryHelper.Distance(players[i].X, players[i].Y, players[j].X, players[j].Y);
                    if (distance < 300) // Only penalty if very clustered
                        penalty -= 0.05f;
                    else if (distance > 10000) // Only penalty if extremely spread out
                        penalty -= 0.05f;
                }
            }

            // Penalty for unreasonable ball targets (shooting when very far from goal)
            var ballDistance = GeometryHelper.Distance(gameState.BallStatus, new BallStatus { X = 12000, Y = 4500 });
            if (ballDistance > 8000)
            {
                // Check if any player is trying to shoot with high velocity
                if (action.P1Instructions.BallVelocity > 80 ||
                    action.P2Instructions.BallVelocity > 80 ||
                    action.P3Instructions.BallVelocity > 80 ||
                    action.P4Instructions.BallVelocity > 80)
                {
                    penalty -= 0.3f; // Reduced penalty
                }
            }

            return penalty;
        }

        private float CalculateTimeBasedReward(GameStatusExtended gameState)
        {
            float reward = 0;

            // Urgency based on score difference and time left
            var scoreDifference = gameState.YourScore - gameState.OpponentScore;
            var timeLeftRatio = gameState.TimeLeftMS / 300000f; // Assuming 5-minute matches

            if (scoreDifference < 0 && timeLeftRatio < 0.2f) // Losing in last 20% of game
            {
                // Reward aggressive attacking play
                if (gameState.YourStatus.P3Status.X > 8000 && gameState.YourStatus.P4Status.X > 8000)
                    reward += 1.0f;
            }
            else if (scoreDifference > 0 && timeLeftRatio < 0.1f) // Winning in last 10% of game
            {
                // Reward defensive play
                if (gameState.YourStatus.P1Status.X < 4000 && gameState.YourStatus.P2Status.X < 4000)
                    reward += 0.5f;
            }

            return reward;
        }

        private float CalculateKickoffBehaviorPenalty(GameStatusExtended gameState, TeamInstructions action)
        {
            float penalty = 0;

            // Detect if this is shortly after kickoff (ball near center and early in game)
            var ballNearCenter = Math.Abs(gameState.BallStatus.X - 6000) < 2000 && 
                                Math.Abs(gameState.BallStatus.Y - 4500) < 2000;
            var timeLeftRatio = gameState.TimeLeftMS / 300000f;
            var isEarlyGame = timeLeftRatio > 0.9f; // First 10% of game

            if (ballNearCenter || isEarlyGame || gameState.KickoffSinceLastUpdate)
            {
                var players = new[]
                {
                    new { X = action.P1Instructions.MoveToX, Y = action.P1Instructions.MoveToY, Id = "P1" },
                    new { X = action.P2Instructions.MoveToX, Y = action.P2Instructions.MoveToY, Id = "P2" },
                    new { X = action.P3Instructions.MoveToX, Y = action.P3Instructions.MoveToY, Id = "P3" },
                    new { X = action.P4Instructions.MoveToX, Y = action.P4Instructions.MoveToY, Id = "P4" }
                };

                foreach (var player in players)
                {
                    // EXTREMELY severe penalty for corner positions during/after kickoff
                    if ((player.X < 1500 && player.Y < 1500) || // Top-left corner
                        (player.X > 10500 && player.Y < 1500) || // Top-right corner
                        (player.X < 1500 && player.Y > 7500) || // Bottom-left corner
                        (player.X > 10500 && player.Y > 7500))   // Bottom-right corner
                    {
                        penalty -= 10.0f; // DOUBLED penalty during kickoff - make it very clear this is bad!
                    }

                    // Additional penalty for edge positions during kickoff
                    if (player.X < 1000 || player.X > 11000 || player.Y < 1000 || player.Y > 8000)
                    {
                        penalty -= 4.0f; // Also doubled
                    }

                    // Penalty for static rectangular formations (players too aligned)
                    for (int i = 0; i < players.Length; i++)
                    {
                        for (int j = i + 1; j < players.Length; j++)
                        {
                            var xDiff = Math.Abs(players[i].X - players[j].X);
                            var yDiff = Math.Abs(players[i].Y - players[j].Y);
                            
                            // Penalty for perfect rectangular alignment (too artificial)
                            if ((xDiff < 100 && yDiff > 2000) || (yDiff < 100 && xDiff > 2000))
                                penalty -= 1.0f;
                        }
                    }
                }

                // Penalty for multiple players in corners simultaneously
                var playersInCorners = players.Count(p => IsPlayerInCorner(p.X, p.Y));
                if (playersInCorners > 1)
                    penalty -= playersInCorners * 8.0f; // Escalating penalty for multiple corner-hiders

                // Extra reward for approaching ball during kickoff
                var ballDistance = players.Min(p => GeometryHelper.Distance(p.X, p.Y, gameState.BallStatus.X, gameState.BallStatus.Y));
                if (ballDistance < 1500) // Someone approaching ball
                    penalty += 3.0f; // Strong positive reward to counter other penalties
            }

            return penalty;
        }

        private float CalculateActivityReward(GameStatusExtended gameState, TeamInstructions action)
        {
            float reward = 0;

            // Reward movement towards ball
            var players = new[]
            {
                gameState.YourStatus.P1Status,
                gameState.YourStatus.P2Status, 
                gameState.YourStatus.P3Status,
                gameState.YourStatus.P4Status
            };

            var ballX = gameState.BallStatus.X;
            var ballY = gameState.BallStatus.Y;

            foreach (var player in players)
            {
                // Current distance to ball
                var currentDistance = GeometryHelper.Distance(player, gameState.BallStatus);
                
                // Strong reward for being in motion (not static)
                var velocity = Math.Sqrt(player.Vx * player.Vx + player.Vy * player.Vy);
                if (velocity > 200) // Moving reasonably fast
                    reward += 2.0f; // Increased from 0.2f to 2.0f
                else if (velocity < 50) // Penalty for being too static
                    reward -= 3.0f; // Increased penalty from 0.3f to 3.0f

                // Strong reward for being in the central area of the field (active zones)
                if (player.X > 2000 && player.X < 10000 && player.Y > 2000 && player.Y < 7000)
                    reward += 3.0f; // Increased from 0.3f to 3.0f
                
                // Extra penalty for being in corner areas (redundant check but important)
                if (IsPlayerInCorner(player.X, player.Y))
                    reward -= 25.0f; // MASSIVE penalty for corner positioning
            }

            // Extra reward if at least one player is close to ball
            var closestDistance = players.Min(p => GeometryHelper.Distance(p, gameState.BallStatus));
            if (closestDistance < 800)
                reward += 1.0f; // Strong reward for ball engagement

            return reward;
        }

        private bool IsPlayerInCorner(int x, int y)
        {
            // Define corner areas
            return (x < 1500 && y < 1500) || // Top-left
                   (x > 10500 && y < 1500) || // Top-right  
                   (x < 1500 && y > 7500) || // Bottom-left
                   (x > 10500 && y > 7500);   // Bottom-right
        }

        private bool WasPlayerInCornerPreviously(PlayerStatus currentPlayer)
        {
            if (_previousState == null) return false;

            // Check if this player (by position similarity) was in corner in previous state
            var prevPlayers = new[] 
            {
                _previousState.YourStatus.P1Status,
                _previousState.YourStatus.P2Status,
                _previousState.YourStatus.P3Status,
                _previousState.YourStatus.P4Status
            };

            // Find the most likely previous position of this player
            var closestPrevPlayer = prevPlayers
                .OrderBy(p => GeometryHelper.Distance(p.X, p.Y, currentPlayer.X, currentPlayer.Y))
                .First();

            return IsPlayerInCorner(closestPrevPlayer.X, closestPrevPlayer.Y);
        }

        public void Reset()
        {
            _previousState = null;
            _previousDistanceToBall = float.MaxValue;
            _previousBallX = 0;
            _previousPlayerPositions.Clear();
            _lastYourScore = null;
            _lastOpponentScore = null;
            _pendingYourGoal = false;
            _pendingOpponentGoal = false;
        }
    }
}
