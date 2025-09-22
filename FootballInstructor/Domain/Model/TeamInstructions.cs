namespace FootballInstructor.Domain.Model
{
    public class TeamInstructions
    {
        /// <summary>
        /// Vänsterback
        /// </summary>
        public PlayerInstructions P1Instructions { get; set; } = default!;

        /// <summary>
        /// Högerback
        /// </summary>
        public PlayerInstructions P2Instructions { get; set; } = default!;

        /// <summary>
        /// Vänsterforward
        /// </summary>
        public PlayerInstructions P3Instructions { get; set; } = default!;

        /// <summary>
        /// Högerforward
        /// </summary>
        public PlayerInstructions P4Instructions { get; set; } = default!;
    }

    public class PlayerInstructions
    {
        /// <summary>
        /// X-koordinat att röra sig mot (cm)
        /// </summary>
        public int MoveToX { get; set; }

        /// <summary>
        /// Y-koordinat att röra sig mot (cm)
        /// </summary>
        public int MoveToY { get; set; }
        
        /// <summary>
        /// Hastighet att röra sig i (0-100)
        /// </summary>
        public int MoveVelocity { get; set; }

        /// <summary>
        /// X-koordinat att skjuta/passa bollen mot (cm)
        /// </summary>
        public int BallTargetX { get; set; }

        /// <summary>
        /// Y-koordinat att skjuta/passa bollen mot (cm)
        /// </summary>
        public int BallTargetY { get; set; }
        
        /// <summary>
        /// Styrka i skott/passning (0-100)
        /// </summary>
        public int BallVelocity { get; set; }
    }
}
