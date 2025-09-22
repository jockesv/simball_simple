namespace FootballInstructor.Domain.Model
{
    public class GameStatus
    {
        /// <summary>
        /// Ditt lags status
        /// </summary>
        public GameTeamStatus YourStatus { get; set; } = default!;

        /// <summary>
        /// Motståndarnas lag's status
        /// </summary>
        public GameTeamStatus OpponentStatus { get; set; } = default!;

        /// <summary>
        /// Bollens status
        /// </summary>
        public BallStatus BallStatus { get; set; } = default!;
    }

    public class GameTeamStatus
    {
        /// <summary>
        /// Vänsterback
        /// </summary>
        public PlayerStatus P1Status { get; set; } = default!;
        
        /// <summary>
        /// Högerback
        /// </summary>
        public PlayerStatus P2Status { get; set; } = default!;

        /// <summary>
        /// Vänsterforward
        /// </summary>
        public PlayerStatus P3Status { get; set; } = default!;

        /// <summary>
        /// Högerback
        /// </summary>
        public PlayerStatus P4Status { get; set; } = default!;
    }

    public class BallStatus : IPoint, IDir
    {
        /// <summary>
        /// Position i X-led (vänster till höger) centimeter 0 - 12000
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Position i Y-led (uppifrån och ner) centimeter 0 - 9000
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Hastighet i X-led cm/s
        /// </summary>
        public int Vx { get; set; }

        /// <summary>
        /// Hastighet i Y-led cm/s
        /// </summary>
        public int Vy { get; set; }
    }

    public class PlayerStatus : IPoint, IDir
    {
        /// <summary>
        /// Position i X-led (vänster till höger) centimeter 0 - 12000
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Position i Y-led (uppifrån och ner) centimeter 0 - 9000
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Hastighet i X-led cm/s
        /// </summary>
        public int Vx { get; set; }

        /// <summary>
        /// Hastighet i Y-led cm/s
        /// </summary>
        public int Vy { get; set; }
    }

    public class GameStatusExtended : GameStatus
    {
        public GameStatusExtended() { }

        public GameStatusExtended(GameStatus gameStatus)
        {
            CorrelationId = Guid.NewGuid().ToString();
            YourStatus = gameStatus.YourStatus;
            OpponentStatus = gameStatus.OpponentStatus;
            BallStatus = gameStatus.BallStatus;
        }

        /// <summary>
        /// Unikt id per lag och match. 
        /// Detta gör att du kan låta samma API styra flera matcher eller båda lagen i en match och ändå hålla isär datat mellan lagen/matcherna ifall du lagrar states.
        /// </summary>
        public string CorrelationId { get; set; } = default!;

        /// <summary>
        /// Matchen är avblåst pga att du gjort mål (läge för segergest?)
        /// </summary>
        public bool YouScored { get; set; }
        
        /// <summary>
        /// Matchen är avblåst pga att motsåndarna gjort mål 
        /// </summary>
        public bool OpponentScored { get; set; }

        /// <summary>
        /// Det har skett en avspark sedan förra uppdateringen 
        /// </summary>
        public bool KickoffSinceLastUpdate { get; set; }

        /// <summary>
        /// Tid kvar av matchen i millisekunder
        /// </summary>
        public int TimeLeftMS { get; set; }

        /// <summary>
        /// Antal mål som ditt lag har i matchen
        /// </summary>
        public int YourScore { get; set; }

        /// <summary>
        /// Antal mål som motståndarlaget har i matchen
        /// </summary>         
        public int OpponentScore { get; set; }

    }


    public interface IPoint
    {
        int X { get; set; }
        int Y { get; set; }
    }

    public interface IDir
    {
        int Vx { get; set; }
        int Vy { get; set; }
    }
}
