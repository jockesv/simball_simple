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
        private bool _hasSpareGaussian = false;
        private float _spareGaussian;

        public PolicyNetwork(int inputSize, int hiddenSize1, int hiddenSize2, int outputSize, float learningRate = 0.0005f, int? seed = null)
        {
            // Policy network uses ScaledTanh for bounded action outputs [0,1]
            _network = new SimpleNeuralNetwork(inputSize, hiddenSize1, hiddenSize2, outputSize, 
                learningRate, 0.9f, seed, OutputActivation.ScaledTanh);
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
        
        private float GenerateGaussian()
        {
            // Box-Muller transform for standard normal distribution
            if (_hasSpareGaussian)
            {
                _hasSpareGaussian = false;
                return _spareGaussian;
            }
            
            _hasSpareGaussian = true;
            float u1 = (float)_random.NextDouble();
            float u2 = (float)_random.NextDouble();
            
            float mag = (float)(Math.Sqrt(-2.0 * Math.Log(u1)));
            _spareGaussian = mag * (float)Math.Cos(2.0 * Math.PI * u2);
            
            return mag * (float)Math.Sin(2.0 * Math.PI * u2);
        }

        /// <summary>
        /// PPO policy gradient update with clipped objective
        /// </summary>
        public void UpdatePolicyGradient(float[] state, float[] action, float advantage, float oldLogProb, float clipEpsilon = 0.2f)
        {
            // Get current policy output
            var currentOutput = _network.Forward(state);
            
            // Calculate current log probability (use same method as storage for consistency)
            var currentLogProb = CalculateLogProb(state, action);
            
            // Check for NaN values before ratio calculation
            if (float.IsNaN(currentLogProb) || float.IsNaN(oldLogProb))
            {
                Console.WriteLine($"[PPO] NaN detected: currentLogProb={currentLogProb}, oldLogProb={oldLogProb}");
                return; // Skip this update
            }
            
            // Calculate probability ratio with bounds checking
            var logRatio = currentLogProb - oldLogProb;
            logRatio = Math.Clamp(logRatio, -10f, 10f); // Prevent extreme ratios
            var ratio = (float)Math.Exp(logRatio);
            
            // PPO clipped objective
            var clippedRatio = Math.Clamp(ratio, 1f - clipEpsilon, 1f + clipEpsilon);
            var weight = Math.Min(ratio * advantage, clippedRatio * advantage); // this is the PPO term
            
            // Convert to gradient target (move toward/away from taken action based on weight sign)
            var target = new float[currentOutput.Length];
            for (int i = 0; i < currentOutput.Length; i++)
            {
                var dir = Math.Sign(weight) * (action[i] - currentOutput[i]); // move toward/away from the taken action
                target[i] = currentOutput[i] + 0.05f * dir;                  // small step
                target[i] = Math.Clamp(target[i], 0f, 1f);
            }
            
            _network.BackwardAndUpdate(state, target);
        }

        private float CalculateLogProbFromOutput(float[] output, float[] action)
        {
            float logProb = 0f;
            float sigma = 0.2f; // Standard deviation
            
            for (int i = 0; i < action.Length; i++)
            {
                float diff = action[i] - output[i];
                logProb -= (diff * diff) / (2 * sigma * sigma);
            }
            
            return logProb;
        }

        /// <summary>
        /// Sample action using squashed Gaussian policy (no clamping)
        /// </summary>
        public float[] SampleAction(float[] policyOutput, float temperature = 1.0f, float sigma = 0.2f)
        {
            var sampledAction = new float[policyOutput.Length];
            
            for (int i = 0; i < policyOutput.Length; i++)
            {
                // Treat network output as μ in ℝ
                var mu = policyOutput[i] * 10f - 5f; // Map [0,1] to [-5,5] for unbounded μ
                
                // Sample u ~ N(μ, σ) in ℝ using Box-Muller transform
                var gaussian = GenerateGaussian();
                var u = mu + gaussian * sigma * temperature;
                
                // Clamp u to prevent extreme tanh values
                u = Math.Clamp(u, -10f, 10f);
                
                // Apply tanh squashing: a = tanh(u) ∈ (-1,1)
                var a_tanh = (float)Math.Tanh(u);
                
                // Map to [0,1]: (a+1)/2
                sampledAction[i] = (a_tanh + 1f) * 0.5f;
            }
            
            return sampledAction;
        }

        /// <summary>
        /// Calculate log probability with proper tanh correction for squashed Gaussian
        /// </summary>
        public float CalculateLogProb(float[] state, float[] action, float sigma = 0.2f)
        {
            var policyOutput = Forward(state);
            
            float logProb = 0f;
            
            for (int i = 0; i < action.Length; i++)
            {
                // Map network output to μ in ℝ 
                var mu = policyOutput[i] * 10f - 5f; // [0,1] → [-5,5]
                
                // Convert action back to tanh space: [0,1] → (-1,1)
                var a_tanh = action[i] * 2f - 1f;
                
                // Clamp to prevent NaN in atanh calculation
                a_tanh = Math.Clamp(a_tanh, -0.9999f, 0.9999f);
                
                // Inverse tanh to get u: u = atanh(a_tanh)
                var u = (float)(0.5 * Math.Log((1 + a_tanh) / (1 - a_tanh)));
                
                // Gaussian log probability: log N(u; μ, σ)
                var diff = u - mu;
                var gaussianLogProb = -(diff * diff) / (2 * sigma * sigma) - 0.5f * (float)Math.Log(2 * Math.PI * sigma * sigma);
                
                // Tanh correction: -log(1 - tanh²(u)) = -log(1 - a_tanh²)
                var tanhCorrection = -(float)Math.Log(1f - a_tanh * a_tanh + 1e-6f); // Add epsilon for numerical stability
                
                logProb += gaussianLogProb + tanhCorrection;
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
            // Value network uses Identity activation for unbounded value outputs
            _network = new SimpleNeuralNetwork(inputSize, hiddenSize1, hiddenSize2, 1, 
                learningRate, 0.9f, seed, OutputActivation.Identity);
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