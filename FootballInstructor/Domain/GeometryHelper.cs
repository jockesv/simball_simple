using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain;

public static class GeometryHelper
{
    public static double Distance(IPoint p1, IPoint p2)
    {
        return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
    }

    public static PlayerStatus GetClosestPlayer(IPoint target, params PlayerStatus[] players)
    {
        PlayerStatus? closestPlayer = null;
        double minDistance = double.MaxValue;

        foreach (var player in players)
        {
            var distance = Distance(target, player);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestPlayer = player;
            }
        }

        return closestPlayer ?? throw new InvalidOperationException("No players provided to find the closest one.");
    }
}