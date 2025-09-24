namespace FootballInstructor.Domain.AI
{
    public class SimpleNeuralNetwork
    {
        private readonly int _inputSize;
        private readonly int _hiddenSize1;
        private readonly int _hiddenSize2;
        private readonly int _outputSize;
        private readonly Random _random;

        // Weights and biases
        private float[,] _weightsInput;
        private float[] _biasHidden1;
        private float[,] _weightsHidden1;
        private float[] _biasHidden2;
        private float[,] _weightsOutput;
        private float[] _biasOutput;

        // For training (gradients)
        private float[,] _gradWeightsInput;
        private float[] _gradBiasHidden1;
        private float[,] _gradWeightsHidden1;
        private float[] _gradBiasHidden2;
        private float[,] _gradWeightsOutput;
        private float[] _gradBiasOutput;

        // Learning parameters
        private readonly float _learningRate;
        private readonly float _momentum;

        // Previous gradients for momentum
        private float[,] _prevGradWeightsInput;
        private float[] _prevGradBiasHidden1;
        private float[,] _prevGradWeightsHidden1;
        private float[] _prevGradBiasHidden2;
        private float[,] _prevGradWeightsOutput;
        private float[] _prevGradBiasOutput;

        public SimpleNeuralNetwork(int inputSize, int hiddenSize1, int hiddenSize2, int outputSize, 
            float learningRate = 0.001f, float momentum = 0.9f, int? seed = null)
        {
            _inputSize = inputSize;
            _hiddenSize1 = hiddenSize1;
            _hiddenSize2 = hiddenSize2;
            _outputSize = outputSize;
            _learningRate = learningRate;
            _momentum = momentum;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();

            InitializeWeights();
            InitializeGradients();
        }

        private void InitializeWeights()
        {
            // Xavier initialization
            var limitInput = Math.Sqrt(6.0 / (_inputSize + _hiddenSize1));
            var limitHidden = Math.Sqrt(6.0 / (_hiddenSize1 + _hiddenSize2));
            var limitOutput = Math.Sqrt(6.0 / (_hiddenSize2 + _outputSize));

            _weightsInput = new float[_inputSize, _hiddenSize1];
            _biasHidden1 = new float[_hiddenSize1];
            for (int i = 0; i < _inputSize; i++)
                for (int j = 0; j < _hiddenSize1; j++)
                    _weightsInput[i, j] = (float)(_random.NextDouble() * 2 * limitInput - limitInput);

            _weightsHidden1 = new float[_hiddenSize1, _hiddenSize2];
            _biasHidden2 = new float[_hiddenSize2];
            for (int i = 0; i < _hiddenSize1; i++)
                for (int j = 0; j < _hiddenSize2; j++)
                    _weightsHidden1[i, j] = (float)(_random.NextDouble() * 2 * limitHidden - limitHidden);

            _weightsOutput = new float[_hiddenSize2, _outputSize];
            _biasOutput = new float[_outputSize];
            for (int i = 0; i < _hiddenSize2; i++)
                for (int j = 0; j < _outputSize; j++)
                    _weightsOutput[i, j] = (float)(_random.NextDouble() * 2 * limitOutput - limitOutput);
        }

        private void InitializeGradients()
        {
            _gradWeightsInput = new float[_inputSize, _hiddenSize1];
            _gradBiasHidden1 = new float[_hiddenSize1];
            _gradWeightsHidden1 = new float[_hiddenSize1, _hiddenSize2];
            _gradBiasHidden2 = new float[_hiddenSize2];
            _gradWeightsOutput = new float[_hiddenSize2, _outputSize];
            _gradBiasOutput = new float[_outputSize];

            _prevGradWeightsInput = new float[_inputSize, _hiddenSize1];
            _prevGradBiasHidden1 = new float[_hiddenSize1];
            _prevGradWeightsHidden1 = new float[_hiddenSize1, _hiddenSize2];
            _prevGradBiasHidden2 = new float[_hiddenSize2];
            _prevGradWeightsOutput = new float[_hiddenSize2, _outputSize];
            _prevGradBiasOutput = new float[_outputSize];
        }

        public float[] Forward(float[] input)
        {
            if (input.Length != _inputSize)
                throw new ArgumentException($"Input size must be {_inputSize}");

            // Hidden layer 1
            var hidden1 = new float[_hiddenSize1];
            for (int j = 0; j < _hiddenSize1; j++)
            {
                hidden1[j] = _biasHidden1[j];
                for (int i = 0; i < _inputSize; i++)
                    hidden1[j] += input[i] * _weightsInput[i, j];
                hidden1[j] = ReLU(hidden1[j]);
            }

            // Hidden layer 2
            var hidden2 = new float[_hiddenSize2];
            for (int j = 0; j < _hiddenSize2; j++)
            {
                hidden2[j] = _biasHidden2[j];
                for (int i = 0; i < _hiddenSize1; i++)
                    hidden2[j] += hidden1[i] * _weightsHidden1[i, j];
                hidden2[j] = ReLU(hidden2[j]);
            }

            // Output layer (using sigmoid for [0,1] bounded output)
            var output = new float[_outputSize];
            for (int j = 0; j < _outputSize; j++)
            {
                output[j] = _biasOutput[j];
                for (int i = 0; i < _hiddenSize2; i++)
                    output[j] += hidden2[i] * _weightsOutput[i, j];
                output[j] = Sigmoid(output[j]);
            }

            return output;
        }

        public void BackwardAndUpdate(float[] input, float[] targetOutput)
        {
            // Forward pass to get intermediate values
            var hidden1 = new float[_hiddenSize1];
            for (int j = 0; j < _hiddenSize1; j++)
            {
                hidden1[j] = _biasHidden1[j];
                for (int i = 0; i < _inputSize; i++)
                    hidden1[j] += input[i] * _weightsInput[i, j];
                hidden1[j] = ReLU(hidden1[j]);
            }

            var hidden2 = new float[_hiddenSize2];
            for (int j = 0; j < _hiddenSize2; j++)
            {
                hidden2[j] = _biasHidden2[j];
                for (int i = 0; i < _hiddenSize1; i++)
                    hidden2[j] += hidden1[i] * _weightsHidden1[i, j];
                hidden2[j] = ReLU(hidden2[j]);
            }

            var output = new float[_outputSize];
            for (int j = 0; j < _outputSize; j++)
            {
                output[j] = _biasOutput[j];
                for (int i = 0; i < _hiddenSize2; i++)
                    output[j] += hidden2[i] * _weightsOutput[i, j];
                output[j] = Sigmoid(output[j]);
            }

            // Backward pass
            // Output layer gradients
            var outputErrors = new float[_outputSize];
            for (int i = 0; i < _outputSize; i++)
            {
                outputErrors[i] = (targetOutput[i] - output[i]) * SigmoidDerivative(output[i]);
                _gradBiasOutput[i] = -outputErrors[i];
                
                for (int j = 0; j < _hiddenSize2; j++)
                    _gradWeightsOutput[j, i] = -outputErrors[i] * hidden2[j];
            }

            // Hidden layer 2 gradients
            var hidden2Errors = new float[_hiddenSize2];
            for (int i = 0; i < _hiddenSize2; i++)
            {
                hidden2Errors[i] = 0;
                for (int j = 0; j < _outputSize; j++)
                    hidden2Errors[i] += outputErrors[j] * _weightsOutput[i, j];
                hidden2Errors[i] *= ReLUDerivative(hidden2[i]);
                
                _gradBiasHidden2[i] = -hidden2Errors[i];
                
                for (int j = 0; j < _hiddenSize1; j++)
                    _gradWeightsHidden1[j, i] = -hidden2Errors[i] * hidden1[j];
            }

            // Hidden layer 1 gradients
            var hidden1Errors = new float[_hiddenSize1];
            for (int i = 0; i < _hiddenSize1; i++)
            {
                hidden1Errors[i] = 0;
                for (int j = 0; j < _hiddenSize2; j++)
                    hidden1Errors[i] += hidden2Errors[j] * _weightsHidden1[i, j];
                hidden1Errors[i] *= ReLUDerivative(hidden1[i]);
                
                _gradBiasHidden1[i] = -hidden1Errors[i];
                
                for (int j = 0; j < _inputSize; j++)
                    _gradWeightsInput[j, i] = -hidden1Errors[i] * input[j];
            }

            // Update weights with momentum
            UpdateWeights();
        }

        private void UpdateWeights()
        {
            // Update input to hidden1 weights
            for (int i = 0; i < _inputSize; i++)
            {
                for (int j = 0; j < _hiddenSize1; j++)
                {
                    var momentum = _momentum * _prevGradWeightsInput[i, j];
                    var update = _learningRate * _gradWeightsInput[i, j] + momentum;
                    _weightsInput[i, j] += update;
                    _prevGradWeightsInput[i, j] = update;
                }
            }

            // Update hidden1 bias
            for (int i = 0; i < _hiddenSize1; i++)
            {
                var momentum = _momentum * _prevGradBiasHidden1[i];
                var update = _learningRate * _gradBiasHidden1[i] + momentum;
                _biasHidden1[i] += update;
                _prevGradBiasHidden1[i] = update;
            }

            // Update hidden1 to hidden2 weights
            for (int i = 0; i < _hiddenSize1; i++)
            {
                for (int j = 0; j < _hiddenSize2; j++)
                {
                    var momentum = _momentum * _prevGradWeightsHidden1[i, j];
                    var update = _learningRate * _gradWeightsHidden1[i, j] + momentum;
                    _weightsHidden1[i, j] += update;
                    _prevGradWeightsHidden1[i, j] = update;
                }
            }

            // Update hidden2 bias
            for (int i = 0; i < _hiddenSize2; i++)
            {
                var momentum = _momentum * _prevGradBiasHidden2[i];
                var update = _learningRate * _gradBiasHidden2[i] + momentum;
                _biasHidden2[i] += update;
                _prevGradBiasHidden2[i] = update;
            }

            // Update hidden2 to output weights
            for (int i = 0; i < _hiddenSize2; i++)
            {
                for (int j = 0; j < _outputSize; j++)
                {
                    var momentum = _momentum * _prevGradWeightsOutput[i, j];
                    var update = _learningRate * _gradWeightsOutput[i, j] + momentum;
                    _weightsOutput[i, j] += update;
                    _prevGradWeightsOutput[i, j] = update;
                }
            }

            // Update output bias
            for (int i = 0; i < _outputSize; i++)
            {
                var momentum = _momentum * _prevGradBiasOutput[i];
                var update = _learningRate * _gradBiasOutput[i] + momentum;
                _biasOutput[i] += update;
                _prevGradBiasOutput[i] = update;
            }
        }

        private static float ReLU(float x) => Math.Max(0, x);
        private static float ReLUDerivative(float x) => x > 0 ? 1 : 0;
        private static float Tanh(float x) => (float)Math.Tanh(x);
        private static float TanhDerivative(float x) => 1 - x * x;
        private static float Sigmoid(float x) => 1f / (1f + (float)Math.Exp(-x));
        private static float SigmoidDerivative(float x) => x * (1f - x);

        public void AddNoise(float noiseFactor = 0.01f)
        {
            // Add small random noise to exploration
            for (int i = 0; i < _inputSize; i++)
                for (int j = 0; j < _hiddenSize1; j++)
                    _weightsInput[i, j] += (float)(_random.NextDouble() * 2 - 1) * noiseFactor;

            for (int i = 0; i < _hiddenSize1; i++)
                for (int j = 0; j < _hiddenSize2; j++)
                    _weightsHidden1[i, j] += (float)(_random.NextDouble() * 2 - 1) * noiseFactor;

            for (int i = 0; i < _hiddenSize2; i++)
                for (int j = 0; j < _outputSize; j++)
                    _weightsOutput[i, j] += (float)(_random.NextDouble() * 2 - 1) * noiseFactor;
        }

        public SimpleNeuralNetwork Clone()
        {
            var clone = new SimpleNeuralNetwork(_inputSize, _hiddenSize1, _hiddenSize2, _outputSize, _learningRate, _momentum);
            
            Array.Copy(_weightsInput, clone._weightsInput, _weightsInput.Length);
            Array.Copy(_biasHidden1, clone._biasHidden1, _biasHidden1.Length);
            Array.Copy(_weightsHidden1, clone._weightsHidden1, _weightsHidden1.Length);
            Array.Copy(_biasHidden2, clone._biasHidden2, _biasHidden2.Length);
            Array.Copy(_weightsOutput, clone._weightsOutput, _weightsOutput.Length);
            Array.Copy(_biasOutput, clone._biasOutput, _biasOutput.Length);
            
            return clone;
        }

        // Methods to access weights for serialization
        public float[,] GetWeightsInput() => (float[,])_weightsInput.Clone();
        public float[] GetBiasHidden1() => (float[])_biasHidden1.Clone();
        public float[,] GetWeightsHidden1() => (float[,])_weightsHidden1.Clone();
        public float[] GetBiasHidden2() => (float[])_biasHidden2.Clone();
        public float[,] GetWeightsOutput() => (float[,])_weightsOutput.Clone();
        public float[] GetBiasOutput() => (float[])_biasOutput.Clone();

        // Methods to set weights from deserialization
        public void SetWeights(float[,] weightsInput, float[] biasHidden1, float[,] weightsHidden1, 
                              float[] biasHidden2, float[,] weightsOutput, float[] biasOutput)
        {
            _weightsInput = (float[,])weightsInput.Clone();
            _biasHidden1 = (float[])biasHidden1.Clone();
            _weightsHidden1 = (float[,])weightsHidden1.Clone();
            _biasHidden2 = (float[])biasHidden2.Clone();
            _weightsOutput = (float[,])weightsOutput.Clone();
            _biasOutput = (float[])biasOutput.Clone();
        }
    }
}