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

    /// <summary>
    /// Kontrollerar om en pass från en spelare till en målposition är blockerad av motståndare
    /// </summary>
    public static bool IsPassBlocked(PlayerStatus passer, int targetX, int targetY, params PlayerStatus[] opponents)
    {
        // Beräkna passlinjen
        var passDistance = Distance(passer.X, passer.Y, targetX, targetY);
        
        // Om passen är väldigt kort, anta att den går igenom
        if (passDistance < 200) // Minska från 300 till 200
        {
            return false;
        }

        // Större blockering-radie för bättre detektering
        var blockingRadius = 120; // Öka från 80 till 120cm

        // Kontrollera varje motståndare
        foreach (var opponent in opponents)
        {
            // Beräkna kortaste avståndet från motståndaren till passlinjen
            var distanceToPassLine = DistanceFromPointToLine(
                opponent.X, opponent.Y,
                passer.X, passer.Y,
                targetX, targetY
            );

            // Förbättrad kontroll - använd projektionsparameter istället för punkt-kontroll
            if (distanceToPassLine < blockingRadius && IsOpponentBlockingPass(passer, targetX, targetY, opponent))
            {
                return true; // Passen är blockerad
            }
        }

        return false; // Passen är fri
    }

    /// <summary>
    /// Förbättrad kontroll för om en motståndare faktiskt blockerar en pass
    /// </summary>
    private static bool IsOpponentBlockingPass(PlayerStatus passer, int targetX, int targetY, PlayerStatus opponent)
    {
        // Beräkna passriktning som vektor
        var passVectorX = targetX - passer.X;
        var passVectorY = targetY - passer.Y;
        
        // Beräkna vektor från passerens position till motståndare
        var toOpponentX = opponent.X - passer.X;
        var toOpponentY = opponent.Y - passer.Y;
        
        // Beräkna passlinjes längd squared (för att undvika sqrt)
        var passLengthSq = passVectorX * passVectorX + passVectorY * passVectorY;
        
        if (passLengthSq == 0) return false; // Ingen pass (samma position)
        
        // Beräkna projektionsparameter t (0 = start, 1 = slut av passlinjen)
        var t = (toOpponentX * passVectorX + toOpponentY * passVectorY) / (double)passLengthSq;
        
        // Motståndaren blockerar bara om den är MELLAN start och slutpunkten
        // Med lite marginal på båda sidor
        return t >= -0.1 && t <= 1.1; // 10% marginal på vardera sidan
    }

    /// <summary>
    /// Beräknar avståndet från en punkt till en linje
    /// </summary>
    private static double DistanceFromPointToLine(int pointX, int pointY, int lineStartX, int lineStartY, int lineEndX, int lineEndY)
    {
        var lineLength = Distance(lineStartX, lineStartY, lineEndX, lineEndY);
        
        if (lineLength == 0)
        {
            return Distance(pointX, pointY, lineStartX, lineStartY);
        }

        var numerator = Math.Abs((lineEndY - lineStartY) * pointX - (lineEndX - lineStartX) * pointY + lineEndX * lineStartY - lineEndY * lineStartX);
        return numerator / lineLength;
    }

    /// <summary>
    /// Projicerar en punkt på en linje och returnerar projektionspunkten
    /// </summary>
    private static (int X, int Y) ProjectPointOntoLine(int pointX, int pointY, int lineStartX, int lineStartY, int lineEndX, int lineEndY)
    {
        var lineVectorX = lineEndX - lineStartX;
        var lineVectorY = lineEndY - lineStartY;
        var pointVectorX = pointX - lineStartX;
        var pointVectorY = pointY - lineStartY;

        var lineLengthSquared = lineVectorX * lineVectorX + lineVectorY * lineVectorY;
        
        if (lineLengthSquared == 0)
        {
            return (lineStartX, lineStartY);
        }

        var t = (pointVectorX * lineVectorX + pointVectorY * lineVectorY) / (double)lineLengthSquared;
        
        var projX = lineStartX + t * lineVectorX;
        var projY = lineStartY + t * lineVectorY;
        
        return ((int)projX, (int)projY);
    }

    /// <summary>
    /// Kontrollerar om en punkt ligger på en linjesegment - FÖRBÄTTRAD VERSION
    /// </summary>
    private static bool IsPointOnLineSegment(int pointX, int pointY, int lineStartX, int lineStartY, int lineEndX, int lineEndY)
    {
        // Beräkna vektorer
        var lineVectorX = lineEndX - lineStartX;
        var lineVectorY = lineEndY - lineStartY;
        var pointVectorX = pointX - lineStartX;
        var pointVectorY = pointY - lineStartY;

        var lineLengthSq = lineVectorX * lineVectorX + lineVectorY * lineVectorY;
        
        if (lineLengthSq == 0)
        {
            // Linje har ingen längd, kolla om punkten är på samma plats
            return Distance(pointX, pointY, lineStartX, lineStartY) < 50; // 50cm tolerans
        }

        // Beräkna projektionsparameter
        var t = (pointVectorX * lineVectorX + pointVectorY * lineVectorY) / (double)lineLengthSq;
        
        // Punkten är på linjesegmentet om t är mellan 0 och 1 (med lite tolerans)
        var tolerance = 0.05; // 5% tolerans
        return t >= -tolerance && t <= 1 + tolerance;
    }

    /// <summary>
    /// Kontrollerar om en spelare kan skjuta/passa i en specifik riktning
    /// Antar att spelaren kan skjuta ~180 grader framåt baserat på sin rörelseriktning
    /// </summary>
    public static bool CanPlayerShootInDirection(PlayerStatus player, int targetX, int targetY)
    {
        // Beräkna spelarens rörelseriktning (om spelaren rör sig)
        var playerSpeed = Math.Sqrt(player.Vx * player.Vx + player.Vy * player.Vy);
        
        // Om spelaren står stilla, anta att den kan skjuta framåt (mot högre X)
        if (playerSpeed < 50)
        {
            return targetX >= player.X; // Kan bara skjuta framåt
        }

        // Beräkna vinkeln som spelaren rör sig i
        var playerAngle = Math.Atan2(player.Vy, player.Vx);
        
        // Beräkna vinkeln till målet
        var targetAngle = Math.Atan2(targetY - player.Y, targetX - player.X);
        
        // Beräkna skillnaden mellan vinklarna
        var angleDifference = Math.Abs(targetAngle - playerAngle);
        
        // Normalisera till 0-π intervall
        if (angleDifference > Math.PI)
        {
            angleDifference = 2 * Math.PI - angleDifference;
        }
        
        // Spelaren kan skjuta inom ~90 grader på vardera sidan (180 grader totalt)
        return angleDifference <= Math.PI / 2;
    }

    /// <summary>
    /// Hittar bästa skottriktning inom spelarens möjliga skottvinkel
    /// </summary>
    public static (int X, int Y) GetBestShootableTarget(PlayerStatus player, int preferredX, int preferredY)
    {
        // Om spelaren kan skjuta direkt mot målet, gör det
        if (CanPlayerShootInDirection(player, preferredX, preferredY))
        {
            return (preferredX, preferredY);
        }

        // Annars, hitta närmaste möjliga skottriktning
        // Prova olika vinklar runt spelarens rörelseriktning
        var playerAngle = Math.Atan2(player.Vy, player.Vx);
        
        // Om spelaren står stilla, rikta framåt
        if (Math.Sqrt(player.Vx * player.Vx + player.Vy * player.Vy) < 50)
        {
            playerAngle = 0; // Rakt framåt (öster)
        }

        // Prova vinklar inom 90-graders intervall
        var bestAngle = playerAngle;
        var targetAngle = Math.Atan2(preferredY - player.Y, preferredX - player.X);
        
        // Välj närmaste möjliga vinkel
        var maxAngleOffset = Math.PI / 2; // 90 grader
        
        if (Math.Abs(targetAngle - playerAngle) <= maxAngleOffset)
        {
            bestAngle = targetAngle;
        }
        else if (targetAngle > playerAngle)
        {
            bestAngle = playerAngle + maxAngleOffset;
        }
        else
        {
            bestAngle = playerAngle - maxAngleOffset;
        }

        // Beräkna ny målposition 2000cm bort i den riktningen
        var shootDistance = 2000;
        var newTargetX = player.X + (int)(Math.Cos(bestAngle) * shootDistance);
        var newTargetY = player.Y + (int)(Math.Sin(bestAngle) * shootDistance);
        
        // Håll inom planens gränser
        newTargetX = Math.Max(0, Math.Min(12000, newTargetX));
        newTargetY = Math.Max(0, Math.Min(9000, newTargetY));
        
        return (newTargetX, newTargetY);
    }
}