using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain.AI
{
    public interface IFootballAI
    {
        TeamInstructions GetTeamInstructions(GameStatusExtended gameStatus);
        void RecordExperience(GameStatusExtended gameState, TeamInstructions action, float reward, GameStatusExtended nextState);
        void Train();
        void SaveModel(string filePath);
        void LoadModel(string filePath);
    }
}