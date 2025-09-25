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


    public PlayerController(ILogger<PlayerController> logger, IPlayerService playerService)
    {
        this.playerService = playerService;
        _logger = logger;
    }

    [HttpPost]
    [Route("update")]
    public TeamInstructions Update(GameStatusExtended gameStatus)
    {
        return this.playerService.Update(gameStatus);
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
