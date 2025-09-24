namespace FootballInstructor.Configuration
{
    public class AISettings
    {
        public string AIType { get; set; } = "Heuristic";
        public string ModelPath { get; set; } = "./models/";
        public bool EnableTraining { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
        public float ExplorationRate { get; set; } = 0.1f;

        // Imitation learning from heuristic AI (behavior cloning) settings
        // When enabled, the RL agent will periodically perform a supervised update
        // towards the heuristic PlayerService's action for the current state.
        public bool ImitationLearningEnabled { get; set; } = false;
        // Perform one imitation update every N environment steps (per instance)
        public int ImitationEveryNSteps { get; set; } = 5;
        // How strongly to move network outputs towards the teacher [0..1]
        public float ImitationWeight { get; set; } = 0.5f;
    }
}