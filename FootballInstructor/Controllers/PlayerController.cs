using FootballInstructor.Domain;
using FootballInstructor.Domain.Model;
using Microsoft.AspNetCore.Mvc;

namespace FootballInstructor.Controllers;

[ApiController]
[Route("[controller]")]
public class PlayerController : ControllerBase
{

    private readonly IPlayerService playerService;
    private readonly ILogger<PlayerController> _logger;
    private static volatile bool _updateInProgress = false;
    private static readonly object _updateLock = new object();


    public PlayerController(ILogger<PlayerController> logger, IPlayerService playerService)
    {
        this.playerService = playerService;
        _logger = logger;
    }

    [HttpPost]
    [Route("update")]
    public TeamInstructions Update(GameStatusExtended gameStatus)
    {
        
        // Check if an update is already in progress
        if (_updateInProgress)
        {
            _logger.LogWarning("Dropping incoming /update request - previous update still in progress");
            // Return a simple fallback instruction to keep the game running
            return GetEmergencyFallbackInstructions(gameStatus);
        }

        lock (_updateLock)
        {
            // Double-check inside the lock
            if (_updateInProgress)
            {
                _logger.LogWarning("Dropping incoming /update request - previous update still in progress (double-check)");
                return GetEmergencyFallbackInstructions(gameStatus);
            }

            try
            {
                _updateInProgress = true;
                ClampVelocities(gameStatus, 500f);
                return this.playerService.Update(gameStatus);
            }
            finally
            {
                _updateInProgress = false;
            }
        }
    }

    [HttpGet]
    [Route("setup")]
    public TeamSetup Setup()
    {
        return this.playerService.Setup();
    }

    [HttpGet]
    public string Test()
    {
        return "Response from server";
    }

    private TeamInstructions GetEmergencyFallbackInstructions(GameStatusExtended gameStatus)
    {
        // Simple emergency fallback - players stay roughly where they are, minimal movement
        var ballX = gameStatus.BallStatus.X;
        var ballY = gameStatus.BallStatus.Y;
        
        return new TeamInstructions
        {
            P1Instructions = new PlayerInstructions
            {
                MoveToX = Math.Max(0, Math.Min(12000, ballX - 500)),
                MoveToY = Math.Max(0, Math.Min(9000, ballY)),
                MoveVelocity = 60,
                BallTargetX = 12000,
                BallTargetY = 4500,
                BallVelocity = 50
            },
            P2Instructions = new PlayerInstructions
            {
                MoveToX = Math.Max(0, Math.Min(12000, ballX - 300)),
                MoveToY = Math.Max(0, Math.Min(9000, ballY)),
                MoveVelocity = 60,
                BallTargetX = 12000,
                BallTargetY = 4500,
                BallVelocity = 50
            },
            P3Instructions = new PlayerInstructions
            {
                MoveToX = Math.Max(0, Math.Min(12000, ballX + 100)),
                MoveToY = Math.Max(0, Math.Min(9000, ballY - 200)),
                MoveVelocity = 70,
                BallTargetX = 12000,
                BallTargetY = 4500,
                BallVelocity = 60
            },
            P4Instructions = new PlayerInstructions
            {
                MoveToX = Math.Max(0, Math.Min(12000, ballX + 100)),
                MoveToY = Math.Max(0, Math.Min(9000, ballY + 200)),
                MoveVelocity = 70,
                BallTargetX = 12000,
                BallTargetY = 4500,
                BallVelocity = 60
            }
        };
    }

    private void ClampVelocities(GameStatusExtended gameStatus, float maxVelocity)
    {
        // Clamp ball velocity
        gameStatus.BallStatus.Vx = (int)Math.Clamp((float)gameStatus.BallStatus.Vx, -maxVelocity, maxVelocity);
        gameStatus.BallStatus.Vy = (int)Math.Clamp((float)gameStatus.BallStatus.Vy, -maxVelocity, maxVelocity);

        // Clamp player velocities for both teams
        ClampTeamVelocities(gameStatus.YourStatus, maxVelocity);
        ClampTeamVelocities(gameStatus.OpponentStatus, maxVelocity);
    }

    private void ClampTeamVelocities(GameTeamStatus teamStatus, float maxVelocity)
    {
        ClampPlayerVelocity(teamStatus.P1Status, maxVelocity);
        ClampPlayerVelocity(teamStatus.P2Status, maxVelocity);
        ClampPlayerVelocity(teamStatus.P3Status, maxVelocity);
        ClampPlayerVelocity(teamStatus.P4Status, maxVelocity);
    }

    private void ClampPlayerVelocity(PlayerStatus playerStatus, float maxVelocity)
    {
        playerStatus.Vx = (int)Math.Clamp((float)playerStatus.Vx, -maxVelocity, maxVelocity);
        playerStatus.Vy = (int)Math.Clamp((float)playerStatus.Vy, -maxVelocity, maxVelocity);
    }

    [HttpGet]
    [Route("force-save-model")]
    public IActionResult ForceSaveModel()
    {
        try
        {
            if (playerService is RLPlayerService rlService)
            {
                var instanceId = rlService.GetInstanceId();
                var modelPath = $"models/rl_model_instance_{instanceId}.json";
                var ai = rlService.GetAI();
                ai.SaveModel(modelPath);
                return Ok($"RL Model saved for instance {instanceId} at {modelPath}");
            }
            else if (playerService is PPOPlayerService ppoService)
            {
                var instanceId = ppoService.GetInstanceId();
                var modelPath = $"models/rl_model_instance_{instanceId}.json";
                var ai = ppoService.GetAI();
                ai.SaveModel(modelPath);
                return Ok($"PPO Model saved for instance {instanceId} at {modelPath}");
            }
            else
            {
                return Ok("Not an RL/PPO service - no model to save");
            }
        }
        catch (Exception ex)
        {
            return BadRequest($"Error saving model: {ex.Message}");
        }
    }
}
