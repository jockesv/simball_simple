using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain
{
    public interface IPlayerService
    {
        TeamInstructions Update(GameStatusExtended gameStatus);
        TeamSetup Setup();

    }
}
