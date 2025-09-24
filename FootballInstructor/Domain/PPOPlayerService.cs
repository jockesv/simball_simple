using FootballInstructor.Domain.Model;
using FootballInstructor.Domain.AI;
using FootballInstructor.Domain.AI.PPO;
using FootballInstructor.Configuration;
using Microsoft.Extensions.Options;

namespace FootballInstructor.Domain
{
    /// <summary>
    /// PPO-based AI player service with imitation learning support
    /// </summary>
    public class PPOPlayerService : IPlayerService
    {
        private readonly IFootballAI _ai;
        private static int _instanceCounter = 0;
        private readonly int _instanceId;
        private readonly AISettings _settings;

        public PPOPlayerService(IOptions<AISettings> options)
        {
            _settings = options.Value ?? new AISettings();
            
            // Derive instance ID from the port this process is running on
            _instanceId = GetInstanceIdFromEnvironment();
            
            // Use different seeds for different instances to ensure diversity
            var seed = _instanceId * 12345 + DateTime.Now.Millisecond;
            _ai = new PPOFootballAI(seed, _instanceId, _settings);
            
            Console.WriteLine($"PPOPlayerService instance {_instanceId} created with seed {seed}");
        }

        private static int GetInstanceIdFromEnvironment()
        {
            // Try to determine instance ID based on port from URLs argument
            var args = Environment.GetCommandLineArgs();
            var urlsArg = args.FirstOrDefault(arg => arg.Contains("--urls=") || arg.Contains("http://localhost:"));
            
            if (urlsArg != null)
            {
                if (urlsArg.Contains("5001")) return 1;
                if (urlsArg.Contains("5002")) return 2;
                if (urlsArg.Contains("5003")) return 3;
                if (urlsArg.Contains("5004")) return 4;
            }
            
            // Fallback to incrementing counter
            return Interlocked.Increment(ref _instanceCounter);
        }

        /// <summary>
        /// Setup team information with unique names for different instances
        /// </summary>
        public TeamSetup Setup()
        {
            var colors = GetTeamColors(_instanceId);
            
            return new TeamSetup()
            {
                Name = $"PPO AI Team {_instanceId}",
                PrimaryColor = colors.Primary,
                SecondaryColor = colors.Secondary
            };
        }

        /// <summary>
        /// Main game update method - uses PPO AI to make decisions
        /// </summary>
        public virtual TeamInstructions Update(GameStatusExtended gameStatus)
        {
            try
            {
                // Let the PPO AI make the decision
                var instructions = _ai.GetTeamInstructions(gameStatus);
                
                // Validate instructions to ensure they're within bounds
                return ValidateInstructions(instructions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PPOPlayerService {_instanceId}: {ex.Message}");
                
                // Fallback to basic behavior if AI fails
                return GetFallbackInstructions(gameStatus);
            }
        }

        private TeamInstructions ValidateInstructions(TeamInstructions instructions)
        {
            return new TeamInstructions
            {
                P1Instructions = ValidatePlayerInstructions(instructions.P1Instructions),
                P2Instructions = ValidatePlayerInstructions(instructions.P2Instructions),
                P3Instructions = ValidatePlayerInstructions(instructions.P3Instructions),
                P4Instructions = ValidatePlayerInstructions(instructions.P4Instructions)
            };
        }

        private PlayerInstructions ValidatePlayerInstructions(PlayerInstructions instructions)
        {
            return new PlayerInstructions
            {
                MoveToX = Math.Clamp(instructions.MoveToX, 0, 12000),
                MoveToY = Math.Clamp(instructions.MoveToY, 0, 9000),
                MoveVelocity = Math.Clamp(instructions.MoveVelocity, 0, 100),
                BallTargetX = Math.Clamp(instructions.BallTargetX, 0, 12000),
                BallTargetY = Math.Clamp(instructions.BallTargetY, 0, 9000),
                BallVelocity = Math.Clamp(instructions.BallVelocity, 0, 100)
            };
        }

        private TeamInstructions GetFallbackInstructions(GameStatusExtended gameStatus)
        {
            // Very simple fallback - all players chase the ball
            var ballX = gameStatus.BallStatus.X;
            var ballY = gameStatus.BallStatus.Y;
            
            return new TeamInstructions
            {
                P1Instructions = new PlayerInstructions
                {
                    MoveToX = Math.Max(0, ballX - 1000),
                    MoveToY = ballY,
                    MoveVelocity = 80,
                    BallTargetX = 12000,
                    BallTargetY = 4500,
                    BallVelocity = 70
                },
                P2Instructions = new PlayerInstructions
                {
                    MoveToX = Math.Max(0, ballX - 800),
                    MoveToY = ballY,
                    MoveVelocity = 80,
                    BallTargetX = 12000,
                    BallTargetY = 4500,
                    BallVelocity = 70
                },
                P3Instructions = new PlayerInstructions
                {
                    MoveToX = ballX + 200,
                    MoveToY = ballY - 300,
                    MoveVelocity = 90,
                    BallTargetX = 12000,
                    BallTargetY = 4500,
                    BallVelocity = 80
                },
                P4Instructions = new PlayerInstructions
                {
                    MoveToX = ballX + 200,
                    MoveToY = ballY + 300,
                    MoveVelocity = 90,
                    BallTargetX = 12000,
                    BallTargetY = 4500,
                    BallVelocity = 80
                }
            };
        }

        private (TeamColors Primary, TeamColors Secondary) GetTeamColors(int instanceId)
        {
            // Different colors for different AI instances to distinguish them
            return (instanceId % 4) switch
            {
                0 => (
                    new TeamColors { Jearsey = "FF0000", Pants = "FFFFFF", Socks = "FF0000" }, // Red
                    new TeamColors { Jearsey = "FFFFFF", Pants = "FF0000", Socks = "FFFFFF" }
                ),
                1 => (
                    new TeamColors { Jearsey = "0000FF", Pants = "FFFF00", Socks = "0000FF" }, // Blue/Yellow (original)
                    new TeamColors { Jearsey = "FFFF00", Pants = "0000FF", Socks = "FFFF00" }
                ),
                2 => (
                    new TeamColors { Jearsey = "00FF00", Pants = "000000", Socks = "00FF00" }, // Green
                    new TeamColors { Jearsey = "000000", Pants = "00FF00", Socks = "000000" }
                ),
                _ => (
                    new TeamColors { Jearsey = "FF00FF", Pants = "FFFFFF", Socks = "FF00FF" }, // Purple
                    new TeamColors { Jearsey = "FFFFFF", Pants = "FF00FF", Socks = "FFFFFF" }
                )
            };
        }

        /// <summary>
        /// Allow external access to the AI for training purposes
        /// </summary>
        public IFootballAI GetAI() => _ai;

        /// <summary>
        /// Get instance ID for identification
        /// </summary>
        public int GetInstanceId() => _instanceId;
    }
}