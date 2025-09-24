namespace FootballInstructor.Configuration
{
    public class AISettings
    {
        public string AIType { get; set; } = "Heuristic"; // "Heuristic", "ReinforcementLearning", "PPO"
        public string ModelPath { get; set; } = "./models/";
        public bool EnableTraining { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
        public float ExplorationRate { get; set; } = 0.1f;

        // Imitation learning from heuristic AI (behavior cloning) settings
        // When enabled, the RL/PPO agent will periodically perform a supervised update
        // towards the heuristic PlayerService's action for the current state.
        public bool ImitationLearningEnabled { get; set; } = false;
        // Perform one imitation update every N environment steps (per instance)
        public int ImitationEveryNSteps { get; set; } = 5;
        // How strongly to move network outputs towards the teacher [0..1]
        public float ImitationWeight { get; set; } = 0.5f;

        // PPO-specific settings
        public float PPOGamma { get; set; } = 0.99f;          // Discount factor
        public float PPOLambda { get; set; } = 0.95f;         // GAE lambda
        public float PPOClipEpsilon { get; set; } = 0.2f;     // PPO clip ratio
        public int PPOEpochs { get; set; } = 4;               // Training epochs per update
        public int PPOBatchSize { get; set; } = 64;           // Mini-batch size
        public int PPOBufferSize { get; set; } = 20;          // Number of trajectories to keep
    }
}