using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain;

public static class PlayerHelper
{
    public static PlayerRole GetPlayerRole(int playerNumber)
    {
        return playerNumber switch
        {
            1 => PlayerRole.Defender,
            2 => PlayerRole.Defender,
            3 => PlayerRole.Attacker,
            4 => PlayerRole.Attacker,
            _ => throw new ArgumentOutOfRangeException(nameof(playerNumber), "Invalid player number")
        };
    }

    public static PlayerStatus GetPlayerStatus(GameStatusExtended gameStatus, int playerNumber)
    {
        return playerNumber switch
        {
            1 => gameStatus.YourStatus.P1Status,
            2 => gameStatus.YourStatus.P2Status,
            3 => gameStatus.YourStatus.P3Status,
            4 => gameStatus.YourStatus.P4Status,
            _ => throw new ArgumentOutOfRangeException(nameof(playerNumber), "Invalid player number")
        };
    }

    public static PlayerStatus GetOpponentStatus(GameStatusExtended gameStatus, int playerNumber)
    {
        return playerNumber switch
        {
            1 => gameStatus.OpponentStatus.P1Status,
            2 => gameStatus.OpponentStatus.P2Status,
            3 => gameStatus.OpponentStatus.P3Status,
            4 => gameStatus.OpponentStatus.P4Status,
            _ => throw new ArgumentOutOfRangeException(nameof(playerNumber), "Invalid player number")
        };
    }

    public static PlayerStatus GetClosestOpponentToBall(GameStatusExtended gameStatus)
    {
        return GeometryHelper.GetClosestPlayer(gameStatus.BallStatus,
            gameStatus.OpponentStatus.P1Status,
            gameStatus.OpponentStatus.P2Status,
            gameStatus.OpponentStatus.P3Status,
            gameStatus.OpponentStatus.P4Status);
    }
}