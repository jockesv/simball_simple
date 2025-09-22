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
}
