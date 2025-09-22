namespace FootballInstructor.Domain.Model
{
    public class TeamSetup
    {
        /// <summary>
        /// Lagets namn
        /// </summary>
        public string Name { get; set; } = default!;

        /// <summary>
        /// Förstaval av färger på kläderna
        /// </summary>
        public TeamColors PrimaryColor { get; set; } = default!;

        /// <summary>
        /// Andrahandsval av färger på kläderna (om andra laget har liknande tröjor)
        /// </summary>
        public TeamColors SecondaryColor { get; set; } = default!;
    }

    public class TeamColors
    {
        /// <summary>
        /// Tröja: webbfärg hex t.ex. 00FF00
        /// </summary>
        public string Jearsey { get; set; } = default!;

        /// <summary>
        /// Byxor: webbfärg hex t.ex. 00FF00
        /// </summary>
        public string Pants { get; set; } = default!;

        /// <summary>
        /// Sockar: webbfärg hex t.ex. 00FF00
        /// </summary>
        public string Socks { get; set; } = default!;
    }
}
