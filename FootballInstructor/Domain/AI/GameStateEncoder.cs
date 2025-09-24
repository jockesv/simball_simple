using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain.AI
{
    public class GameExperience
    {
        public float[] State { get; set; } = default!;
        public float[] Action { get; set; } = default!;
        public float Reward { get; set; }
        public float[] NextState { get; set; } = default!;
        public bool IsTerminal { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class GameStateEncoder
    {
        private const int STATE_SIZE = 40; // 8 players * 4 features + ball * 4 + game info * 4
        private const int ACTION_SIZE = 24; // 4 players * 6 actions each

        public float[] EncodeGameState(GameStatusExtended gameStatus)
        {
            var state = new float[STATE_SIZE];
            var index = 0;

            // Our team positions and velocities (normalized)
            state[index++] = gameStatus.YourStatus.P1Status.X / 12000f;
            state[index++] = gameStatus.YourStatus.P1Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.YourStatus.P1Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.YourStatus.P1Status.Vy / 1000f, -1f, 1f);

            state[index++] = gameStatus.YourStatus.P2Status.X / 12000f;
            state[index++] = gameStatus.YourStatus.P2Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.YourStatus.P2Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.YourStatus.P2Status.Vy / 1000f, -1f, 1f);

            state[index++] = gameStatus.YourStatus.P3Status.X / 12000f;
            state[index++] = gameStatus.YourStatus.P3Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.YourStatus.P3Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.YourStatus.P3Status.Vy / 1000f, -1f, 1f);

            state[index++] = gameStatus.YourStatus.P4Status.X / 12000f;
            state[index++] = gameStatus.YourStatus.P4Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.YourStatus.P4Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.YourStatus.P4Status.Vy / 1000f, -1f, 1f);

            // Opponent team positions and velocities
            state[index++] = gameStatus.OpponentStatus.P1Status.X / 12000f;
            state[index++] = gameStatus.OpponentStatus.P1Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P1Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P1Status.Vy / 1000f, -1f, 1f);

            state[index++] = gameStatus.OpponentStatus.P2Status.X / 12000f;
            state[index++] = gameStatus.OpponentStatus.P2Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P2Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P2Status.Vy / 1000f, -1f, 1f);

            state[index++] = gameStatus.OpponentStatus.P3Status.X / 12000f;
            state[index++] = gameStatus.OpponentStatus.P3Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P3Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P3Status.Vy / 1000f, -1f, 1f);

            state[index++] = gameStatus.OpponentStatus.P4Status.X / 12000f;
            state[index++] = gameStatus.OpponentStatus.P4Status.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P4Status.Vx / 1000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.OpponentStatus.P4Status.Vy / 1000f, -1f, 1f);

            // Ball position and velocity
            state[index++] = gameStatus.BallStatus.X / 12000f;
            state[index++] = gameStatus.BallStatus.Y / 9000f;
            state[index++] = Math.Clamp(gameStatus.BallStatus.Vx / 2000f, -1f, 1f);
            state[index++] = Math.Clamp(gameStatus.BallStatus.Vy / 2000f, -1f, 1f);

            // Game state information
            state[index++] = gameStatus.YourScore / 10f; // Normalize scores
            state[index++] = gameStatus.OpponentScore / 10f;
            state[index++] = gameStatus.TimeLeftMS / 300000f; // 5 min match
            state[index++] = gameStatus.KickoffSinceLastUpdate ? 1f : 0f;

            return state;
        }

        public float[] EncodeAction(TeamInstructions instructions)
        {
            var action = new float[ACTION_SIZE];
            var index = 0;

            // Player 1 instructions
            action[index++] = instructions.P1Instructions.MoveToX / 12000f;
            action[index++] = instructions.P1Instructions.MoveToY / 9000f;
            action[index++] = instructions.P1Instructions.MoveVelocity / 100f;
            action[index++] = instructions.P1Instructions.BallTargetX / 12000f;
            action[index++] = instructions.P1Instructions.BallTargetY / 9000f;
            action[index++] = instructions.P1Instructions.BallVelocity / 100f;

            // Player 2 instructions
            action[index++] = instructions.P2Instructions.MoveToX / 12000f;
            action[index++] = instructions.P2Instructions.MoveToY / 9000f;
            action[index++] = instructions.P2Instructions.MoveVelocity / 100f;
            action[index++] = instructions.P2Instructions.BallTargetX / 12000f;
            action[index++] = instructions.P2Instructions.BallTargetY / 9000f;
            action[index++] = instructions.P2Instructions.BallVelocity / 100f;

            // Player 3 instructions
            action[index++] = instructions.P3Instructions.MoveToX / 12000f;
            action[index++] = instructions.P3Instructions.MoveToY / 9000f;
            action[index++] = instructions.P3Instructions.MoveVelocity / 100f;
            action[index++] = instructions.P3Instructions.BallTargetX / 12000f;
            action[index++] = instructions.P3Instructions.BallTargetY / 9000f;
            action[index++] = instructions.P3Instructions.BallVelocity / 100f;

            // Player 4 instructions
            action[index++] = instructions.P4Instructions.MoveToX / 12000f;
            action[index++] = instructions.P4Instructions.MoveToY / 9000f;
            action[index++] = instructions.P4Instructions.MoveVelocity / 100f;
            action[index++] = instructions.P4Instructions.BallTargetX / 12000f;
            action[index++] = instructions.P4Instructions.BallTargetY / 9000f;
            action[index++] = instructions.P4Instructions.BallVelocity / 100f;

            return action;
        }

        public TeamInstructions DecodeAction(float[] action)
        {
            var index = 0;

            return new TeamInstructions
            {
                P1Instructions = new PlayerInstructions
                {
                    MoveToX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    MoveToY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    MoveVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100),
                    BallTargetX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    BallTargetY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    BallVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100)
                },
                P2Instructions = new PlayerInstructions
                {
                    MoveToX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    MoveToY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    MoveVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100),
                    BallTargetX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    BallTargetY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    BallVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100)
                },
                P3Instructions = new PlayerInstructions
                {
                    MoveToX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    MoveToY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    MoveVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100),
                    BallTargetX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    BallTargetY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    BallVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100)
                },
                P4Instructions = new PlayerInstructions
                {
                    MoveToX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    MoveToY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    MoveVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100),
                    BallTargetX = (int)(Math.Clamp(action[index++], 0f, 1f) * 12000),
                    BallTargetY = (int)(Math.Clamp(action[index++], 0f, 1f) * 9000),
                    BallVelocity = (int)(Math.Clamp(action[index++], 0f, 1f) * 100)
                }
            };
        }

        public static int StateSize => STATE_SIZE;
        public static int ActionSize => ACTION_SIZE;
    }
}