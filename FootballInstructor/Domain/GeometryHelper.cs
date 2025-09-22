using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain;

public static class GeometryHelper
{
    public static double Distance(IPoint p1, IPoint p2)
    {
        return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
    }

    public static double Distance(int x1, int y1, int x2, int y2)
    {
        return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
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

    /// <summary>
    /// Beräknar var bollen kommer att vara efter en viss tid baserat på dess hastighet
    /// </summary>
    public static (int X, int Y) PredictBallPosition(BallStatus ball, double timeInSeconds)
    {
        // Enkla fysikberäkningar: position = nuvarande + hastighet * tid
        // Antar att friktionen bromsar bollen med cirka 20% per sekund
        var frictionFactor = Math.Pow(0.8, timeInSeconds);
        
        var predictedVx = ball.Vx * frictionFactor;
        var predictedVy = ball.Vy * frictionFactor;
        
        var futureX = ball.X + (int)(predictedVx * timeInSeconds);
        var futureY = ball.Y + (int)(predictedVy * timeInSeconds);
        
        // Håll inom planens gränser
        futureX = Math.Max(0, Math.Min(12000, futureX));
        futureY = Math.Max(0, Math.Min(9000, futureY));
        
        return (futureX, futureY);
    }

    /// <summary>
    /// Beräknar optimal intercept-position för en spelare att möta bollen
    /// </summary>
    public static (int X, int Y) CalculateInterceptPosition(PlayerStatus player, BallStatus ball)
    {
        var ballSpeed = Math.Sqrt(ball.Vx * ball.Vx + ball.Vy * ball.Vy);
        
        // Om bollen står stilla eller rör sig väldigt långsamt, gå direkt till den
        if (ballSpeed < 50)
        {
            return (ball.X, ball.Y);
        }
        
        // Beräkna olika tider och hitta bästa intercept-punkten
        var bestTime = 0.0;
        var bestDistance = double.MaxValue;
        
        for (double time = 0.1; time <= 3.0; time += 0.2) // Kolla 0.1 till 3 sekunder framåt
        {
            var predictedBall = PredictBallPosition(ball, time);
            var distanceToIntercept = Distance(player, new BallStatus { X = predictedBall.X, Y = predictedBall.Y });
            
            // Spelarhastighet: ca 700 cm/s vid max hastighet
            var playerTravelTime = distanceToIntercept / 700.0; // sekunder att nå positionen
            
            // Om spelaren kan nå positionen i tid (med lite marginal)
            if (playerTravelTime <= time + 0.2)
            {
                if (distanceToIntercept < bestDistance)
                {
                    bestDistance = distanceToIntercept;
                    bestTime = time;
                }
            }
        }
        
        // Returnera bästa intercept-position
        return PredictBallPosition(ball, bestTime);
    }
}