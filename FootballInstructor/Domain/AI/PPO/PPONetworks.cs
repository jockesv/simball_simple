using System;

namespace FootballInstructor.Domain.AI.PPO
{
    /// <summary>
    /// Policy network that outputs action probabilities (stochastic policy)
    /// </summary>
    public class PolicyNetwork
    {
        private readonly SimpleNeuralNetwork _network;
        private readonly Random _random;

        public PolicyNetwork(int inputSize, int hiddenSize1, int hiddenSize2, int outputSize, float learningRate = 0.0005f, int? seed = null)
        {
            // Reduced learning rate for more stable training
            _network = new SimpleNeuralNetwork(inputSize, hiddenSize1, hiddenSize2, outputSize, learningRate, 0.9f, seed);
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public float[] Forward(float[] state)
        {
            return _network.Forward(state);
        }

        public void BackwardAndUpdate(float[] state, float[] targetProbs)
        {
            _network.BackwardAndUpdate(state, targetProbs);
        }

        /// <summary>
        /// Sample action from policy probabilities with exploration
        /// </summary>
        public float[] SampleAction(float[] actionProbs, float temperature = 1.0f)
        {
            var sampledAction = new float[actionProbs.Length];
            
            // Add temperature-controlled noise for exploration
            for (int i = 0; i < actionProbs.Length; i++)
            {
                var noise = (float)(_random.NextDouble() * 2 - 1) * temperature * 0.1f;
                sampledAction[i] = Math.Clamp(actionProbs[i] + noise, 0f, 1f);
            }
            
            return sampledAction;
        }

        /// <summary>
        /// Calculate log probability of taking specific action under current policy
        /// </summary>
        public float CalculateLogProb(float[] state, float[] action)
        {
            var policyOutput = Forward(state);
            
            // Simple Gaussian log probability (for continuous actions)
            float logProb = 0f;
            float sigma = 0.2f; // Standard deviation
            
            for (int i = 0; i < action.Length; i++)
            {
                float diff = action[i] - policyOutput[i];
                logProb -= (diff * diff) / (2 * sigma * sigma);
            }
            
            return logProb;
        }
    }

    /// <summary>
    /// Value network that estimates state values for GAE computation
    /// </summary>
    public class ValueNetwork
    {
        private readonly SimpleNeuralNetwork _network;

        public ValueNetwork(int inputSize, int hiddenSize1, int hiddenSize2, float learningRate = 0.0005f, int? seed = null)
        {
            // Value network outputs single value - reduced learning rate for stability
            _network = new SimpleNeuralNetwork(inputSize, hiddenSize1, hiddenSize2, 1, learningRate, 0.9f, seed);
        }

        public float EstimateValue(float[] state)
        {
            var output = _network.Forward(state);
            return output[0];
        }

        public void UpdateValue(float[] state, float targetValue)
        {
            var target = new float[] { targetValue };
            _network.BackwardAndUpdate(state, target);
        }
    }
}