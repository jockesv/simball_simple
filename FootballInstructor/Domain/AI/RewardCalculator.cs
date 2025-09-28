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

            // Add shaped rewards
            reward += CalculateBallPossessionReward(gameState);
            reward += CalculateBallAdvancementReward(gameState);
            reward += CalculateActivityReward(gameState);

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

            // Reward for being closer to the ball than the opponent
            if (ourDistance < opponentDistance)
            {
                // Scale reward by how much closer we are, maxing out at 1.0
                return (float)Math.Min(1.0f, (opponentDistance - ourDistance) / 1000f);
            }

            return -0.1f; // Small penalty for not having possession
        }

        private float CalculateBallAdvancementReward(GameStatusExtended gameState)
        {
            // Reward for advancing ball towards opponent goal
            var ballAdvancement = gameState.BallStatus.X - _previousBallX;
            _previousBallX = gameState.BallStatus.X;

            if (ballAdvancement > 0) // Ball moved towards opponent goal
            {
                return (float)Math.Min(ballAdvancement / 500f, 0.5f); // Max 0.5 reward
            }

            return 0;
        }

        private float CalculateActivityReward(GameStatusExtended gameState)
        {
            float totalVelocity = 0;
            var players = new[]
            {
                gameState.YourStatus.P1Status,
                gameState.YourStatus.P2Status,
                gameState.YourStatus.P3Status,
                gameState.YourStatus.P4Status
            };

            foreach (var player in players)
            {
                totalVelocity += (float)(Math.Abs(player.Vx) + Math.Abs(player.Vy));
            }

            // Small reward for movement, scaled by total velocity. Max around 0.2
            return (float)Math.Min(0.2f, totalVelocity / 20000f);
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
