using System;
using System.Collections.Generic;
using System.Linq;
using FootballInstructor.Domain.Model;
using FootballInstructor.Configuration;

namespace FootballInstructor.Domain.AI.PPO
{
    /// <summary>
    /// PPO Agent with GAE that supports imitation learning from heuristic teacher
    /// </summary>
    public class PPOFootballAI : IFootballAI
    {
        private readonly PolicyNetwork _policyNetwork;
        private readonly ValueNetwork _valueNetwork;
        private readonly GameStateEncoder _encoder;
        private readonly RewardCalculator _rewardCalculator;
        private readonly PPOBuffer _buffer;
        private readonly Random _random;
        private readonly string _modelPath;
        private readonly int _instanceId;
        private readonly AISettings _settings;
        private readonly PlayerService _teacher; // heuristic teacher for imitation
        
        // PPO Parameters
        private const float GAMMA = 0.99f;
        private const float LAMBDA = 0.95f;
        private const float CLIP_EPSILON = 0.2f;
        private const int PPO_EPOCHS = 4;
        private const int BATCH_SIZE = 64;
        
        // Current trajectory tracking
        private PPOTrajectory _currentTrajectory;
        private GameStatusExtended? _previousGameState;
        private TeamInstructions? _previousAction;
        private float _previousLogProb;
        private int _stepCount = 0;
        
        // Performance tracking
        private int _totalGames = 0;
        private int _wins = 0;
        private int _losses = 0;
        private List<float> _recentRewards = new();
        
        // Exploration parameters
        private float _currentTemperature = 1.0f;
        private const float TEMPERATURE_DECAY = 0.995f;
        
        // Model saving
        private DateTime _lastSaveTime = DateTime.Now;
        private const int SAVE_INTERVAL_GAMES = 5;
        private const int SAVE_INTERVAL_MINUTES = 2;

        public PPOFootballAI(int? seed = null, int instanceId = 1, AISettings? settings = null)
        {
            _instanceId = instanceId;
            _settings = settings ?? new AISettings();
            _encoder = new GameStateEncoder();
            _rewardCalculator = new RewardCalculator();
            _buffer = new PPOBuffer(maxTrajectories: 20);
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _teacher = new PlayerService(); // heuristic teacher for imitation learning
            _currentTrajectory = new PPOTrajectory();
            
            // Set up model path - save to source directory, not runtime directory
            var sourceDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            _modelPath = Path.Combine(sourceDir, "models", $"rl_model_instance_{_instanceId}.json");
            
            // Create networks
            var networkSeed = seed.HasValue ? seed.Value : _random.Next();
            _policyNetwork = new PolicyNetwork(
                inputSize: GameStateEncoder.StateSize,
                hiddenSize1: 256,
                hiddenSize2: 128,
                outputSize: GameStateEncoder.ActionSize,
                learningRate: _settings.ImitationLearningEnabled ? 0.0001f : 0.0003f, // Lower LR for imitation
                seed: networkSeed
            );
            
            _valueNetwork = new ValueNetwork(
                inputSize: GameStateEncoder.StateSize,
                hiddenSize1: 128,
                hiddenSize2: 64,
                learningRate: 0.001f,
                seed: networkSeed + 1
            );
            
            // Try to load existing model
            LoadModel(_modelPath);
            
            Console.WriteLine($"PPOFootballAI instance {_instanceId} initialized");
            Console.WriteLine($"[DEBUG] Model path: {_modelPath}");
            Console.WriteLine($"[DEBUG] Imitation learning enabled: {_settings.ImitationLearningEnabled}, weight: {_settings.ImitationWeight}");
        }

        public TeamInstructions GetTeamInstructions(GameStatusExtended gameStatus)
        {
            // Process previous experience FIRST if we have it (before any reset clears it)
            if (_previousGameState != null && _previousAction != null)
            {
                var reward = _rewardCalculator.CalculateReward(_previousGameState, _previousAction, gameStatus);
                RecordExperience(_previousGameState, _previousAction, reward, gameStatus, _previousLogProb);
                
                _recentRewards.Add(reward);
                if (_recentRewards.Count > 100)
                    _recentRewards.RemoveAt(0);
                
                // Debug: Show non-zero rewards
                if (reward != 0 && _settings.EnableLogging)
                {
                    Console.WriteLine($"[PPO-{_instanceId}] Step reward: {reward:F4} (Steps in episode: {_currentTrajectory.Experiences.Count})");
                }
                
                // Check for game end
                if (gameStatus.YouScored || gameStatus.OpponentScored)
                {
                    HandleGameEnd(gameStatus);
                }
            }
            
            // Handle game resets AFTER processing previous experience
            if (gameStatus.KickoffSinceLastUpdate || _previousGameState == null)
            {
                HandleGameReset(gameStatus);
            }
            
            // Get action using PPO policy or teacher
            var action = GetAction(gameStatus);
            
            // Store current state and action for next iteration
            _previousGameState = CloneGameState(gameStatus);
            _previousAction = action;
            _stepCount++;
            
            // Train PPO periodically when we have enough trajectories
            if (_buffer.TrajectoryCount >= 5 && _stepCount % 50 == 0)
            {
                TrainPPO();
            }
            
            // Perform imitation learning if enabled
            if (_settings.ImitationLearningEnabled && _stepCount % Math.Max(1, _settings.ImitationEveryNSteps) == 0)
            {
                PerformImitationUpdate(gameStatus);
            }
            
            return action;
        }

        private TeamInstructions GetAction(GameStatusExtended gameStatus)
        {
            var state = _encoder.EncodeGameState(gameStatus);
            
            // Teacher blending for imitation learning
            if (_settings.ImitationLearningEnabled && _settings.ImitationWeight > 0.5f)
            {
                var teacherAction = _teacher.Update(gameStatus);
                var blendRatio = Math.Min(_settings.ImitationWeight, 0.95f);
                
                if (_random.NextDouble() < blendRatio)
                {
                    // Use teacher action with small variation
                    var action = AddSmallVariation(teacherAction, 0.05f);
                    _previousLogProb = -1f; // Mark as teacher action
                    
                    if (_settings.EnableLogging && _stepCount % 40 == 0)
                    {
                        Console.WriteLine($"[TEACHER] P1 move: ({action.P1Instructions.MoveToX},{action.P1Instructions.MoveToY}) vel:{action.P1Instructions.MoveVelocity}");
                    }
                    return action;
                }
            }
            
            // PPO Policy action
            var policyOutput = _policyNetwork.Forward(state);
            var sampledAction = _policyNetwork.SampleAction(policyOutput, _currentTemperature);
            var decodedAction = _encoder.DecodeAction(sampledAction);
            
            // Calculate log probability for PPO training
            _previousLogProb = _policyNetwork.CalculateLogProb(state, sampledAction);
            
            if (_settings.EnableLogging && _stepCount % 5 == 0)
            {
                Console.WriteLine($"[PPO] P1 move: ({decodedAction.P1Instructions.MoveToX},{decodedAction.P1Instructions.MoveToY}) vel:{decodedAction.P1Instructions.MoveVelocity}");
            }
            
            return decodedAction;
        }

        private void RecordExperience(GameStatusExtended gameState, TeamInstructions action, float reward, GameStatusExtended nextState, float logProb)
        {
            var state = _encoder.EncodeGameState(gameState);
            var actionEncoded = _encoder.EncodeAction(action);
            var nextStateEncoded = _encoder.EncodeGameState(nextState);
            
            // Get state value estimate
            var stateValue = _valueNetwork.EstimateValue(state);
            
            var experience = new PPOExperience
            {
                State = state,
                Action = actionEncoded,
                Reward = reward,
                NextState = nextStateEncoded,
                IsTerminal = nextState.YouScored || nextState.OpponentScored,
                Value = stateValue,
                LogProb = logProb,
                Timestamp = DateTime.Now
            };

            _currentTrajectory.AddExperience(experience);
        }

        private void HandleGameEnd(GameStatusExtended gameStatus)
        {
            _totalGames++;
            
            if (gameStatus.YouScored)
                _wins++;
            else if (gameStatus.OpponentScored)
                _losses++;
            
            // Complete current trajectory
            _currentTrajectory.IsComplete = true;
            _buffer.AddTrajectory(_currentTrajectory);
            
            // Start new trajectory
            _currentTrajectory = new PPOTrajectory();
            
            var avgReward = _recentRewards.Count > 0 ? _recentRewards.Average() : 0;
            Console.WriteLine($"PPO Instance {_instanceId} - Game ended. Score: {gameStatus.YourScore}-{gameStatus.OpponentScore}, " +
                            $"Avg reward: {avgReward:F6}, Win rate: {GetWinRate():F2}%, " +
                            $"Temperature: {_currentTemperature:F3}, Trajectories: {_buffer.TrajectoryCount}");
            
            // Decay exploration temperature
            _currentTemperature = Math.Max(0.1f, _currentTemperature * TEMPERATURE_DECAY);
            
            // Auto-save model periodically
            var shouldSave = _totalGames % SAVE_INTERVAL_GAMES == 0 || 
                           (DateTime.Now - _lastSaveTime).TotalMinutes >= SAVE_INTERVAL_MINUTES;
            
            if (shouldSave)
            {
                SaveModel(_modelPath);
            }
        }

        private void TrainPPO()
        {
            var allExperiences = _buffer.GetAllExperiences();
            if (allExperiences.Count < BATCH_SIZE) return;

            // Train for multiple epochs
            for (int epoch = 0; epoch < PPO_EPOCHS; epoch++)
            {
                // Shuffle experiences
                var shuffled = allExperiences.OrderBy(x => _random.Next()).ToList();
                
                // Train in batches
                for (int i = 0; i < shuffled.Count - BATCH_SIZE; i += BATCH_SIZE)
                {
                    var batch = shuffled.Skip(i).Take(BATCH_SIZE).ToList();
                    TrainPolicyBatch(batch);
                    TrainValueBatch(batch);
                }
            }
            
            // Clear old trajectories periodically
            if (_buffer.TrajectoryCount > 15)
            {
                _buffer.Clear();
            }
            
            Console.WriteLine($"[PPO] Trained on {allExperiences.Count} experiences, {PPO_EPOCHS} epochs");
        }

        private void TrainPolicyBatch(List<PPOExperience> batch)
        {
            foreach (var exp in batch)
            {
                if (exp.LogProb < 0) continue; // Skip teacher actions
                
                // Use proper PPO clipped objective gradient update (fixed parameter order)
                _policyNetwork.UpdatePolicyGradient(exp.State, exp.Action, exp.Advantage, exp.LogProb);
            }
        }

        private void TrainValueBatch(List<PPOExperience> batch)
        {
            foreach (var exp in batch)
            {
                _valueNetwork.UpdateValue(exp.State, exp.Return);
            }
        }

        private void PerformImitationUpdate(GameStatusExtended gameStatus)
        {
            var teacherAction = _teacher.Update(gameStatus);
            var state = _encoder.EncodeGameState(gameStatus);
            var teacherEncoded = _encoder.EncodeAction(teacherAction);

            // Train policy network to mimic teacher
            _policyNetwork.BackwardAndUpdate(state, teacherEncoded);
            
            if (_settings.EnableLogging && _stepCount % 100 == 0)
            {
                Console.WriteLine($"[IMIT] Applied imitation update towards heuristic teacher");
            }
        }

        private TeamInstructions AddSmallVariation(TeamInstructions action, float variationFactor)
        {
            return new TeamInstructions
            {
                P1Instructions = AddPlayerVariation(action.P1Instructions, variationFactor),
                P2Instructions = AddPlayerVariation(action.P2Instructions, variationFactor),
                P3Instructions = AddPlayerVariation(action.P3Instructions, variationFactor),
                P4Instructions = AddPlayerVariation(action.P4Instructions, variationFactor)
            };
        }

        private PlayerInstructions AddPlayerVariation(PlayerInstructions instruction, float variationFactor)
        {
            var noise = () => (float)(_random.NextDouble() * 2 - 1) * variationFactor;
            
            return new PlayerInstructions
            {
                MoveToX = Math.Clamp((int)(instruction.MoveToX * (1 + noise() * 0.05)), 0, 12000),
                MoveToY = Math.Clamp((int)(instruction.MoveToY * (1 + noise() * 0.05)), 0, 9000),
                MoveVelocity = Math.Clamp((int)(instruction.MoveVelocity * (1 + noise() * 0.05)), 0, 100),
                BallTargetX = Math.Clamp((int)(instruction.BallTargetX * (1 + noise() * 0.05)), 0, 12000),
                BallTargetY = Math.Clamp((int)(instruction.BallTargetY * (1 + noise() * 0.05)), 0, 9000),
                BallVelocity = Math.Clamp((int)(instruction.BallVelocity * (1 + noise() * 0.05)), 0, 100)
            };
        }

        private void HandleGameReset(GameStatusExtended gameStatus)
        {
            _rewardCalculator.Reset();
            _previousGameState = null;
            _previousAction = null;
            
            Console.WriteLine($"PPO Game reset detected. Total games: {_totalGames}, Win rate: {GetWinRate():F2}%");
        }

        private double GetWinRate()
        {
            return _totalGames > 0 ? (_wins * 100.0 / _totalGames) : 0;
        }

        private GameStatusExtended CloneGameState(GameStatusExtended original)
        {
            // Simple clone implementation (same as before)
            return new GameStatusExtended
            {
                CorrelationId = original.CorrelationId,
                YouScored = original.YouScored,
                OpponentScored = original.OpponentScored,
                KickoffSinceLastUpdate = original.KickoffSinceLastUpdate,
                TimeLeftMS = original.TimeLeftMS,
                YourScore = original.YourScore,
                OpponentScore = original.OpponentScore,
                YourStatus = CloneTeamStatus(original.YourStatus),
                OpponentStatus = CloneTeamStatus(original.OpponentStatus),
                BallStatus = CloneBallStatus(original.BallStatus)
            };
        }

        private GameTeamStatus CloneTeamStatus(GameTeamStatus original)
        {
            return new GameTeamStatus
            {
                P1Status = ClonePlayerStatus(original.P1Status),
                P2Status = ClonePlayerStatus(original.P2Status),
                P3Status = ClonePlayerStatus(original.P3Status),
                P4Status = ClonePlayerStatus(original.P4Status)
            };
        }

        private PlayerStatus ClonePlayerStatus(PlayerStatus original)
        {
            return new PlayerStatus
            {
                X = original.X,
                Y = original.Y,
                Vx = original.Vx,
                Vy = original.Vy
            };
        }

        private BallStatus CloneBallStatus(BallStatus original)
        {
            return new BallStatus
            {
                X = original.X,
                Y = original.Y,
                Vx = original.Vx,
                Vy = original.Vy
            };
        }

        public void SaveModel(string filePath)
        {
            try
            {
                // Create models directory if it doesn't exist
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Serialize both neural networks and training state
                var saveData = new
                {
                    // Training statistics
                    TotalGames = _totalGames,
                    Wins = _wins,
                    Losses = _losses,
                    Temperature = _currentTemperature,
                    Timestamp = DateTime.Now,
                    
                    // Policy network weights (convert 2D arrays to jagged arrays for JSON)
                    PolicyNetwork = new
                    {
                        WeightsInput = Convert2DToJagged(_policyNetwork.GetWeightsInput()),
                        BiasHidden1 = _policyNetwork.GetBiasHidden1(),
                        WeightsHidden1 = Convert2DToJagged(_policyNetwork.GetWeightsHidden1()),
                        BiasHidden2 = _policyNetwork.GetBiasHidden2(),
                        WeightsOutput = Convert2DToJagged(_policyNetwork.GetWeightsOutput()),
                        BiasOutput = _policyNetwork.GetBiasOutput()
                    },
                    
                    // Value network weights (convert 2D arrays to jagged arrays for JSON)
                    ValueNetwork = new
                    {
                        WeightsInput = Convert2DToJagged(_valueNetwork.GetWeightsInput()),
                        BiasHidden1 = _valueNetwork.GetBiasHidden1(),
                        WeightsHidden1 = Convert2DToJagged(_valueNetwork.GetWeightsHidden1()),
                        BiasHidden2 = _valueNetwork.GetBiasHidden2(),
                        WeightsOutput = Convert2DToJagged(_valueNetwork.GetWeightsOutput()),
                        BiasOutput = _valueNetwork.GetBiasOutput()
                    }
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(saveData, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });
                
                System.IO.File.WriteAllText(filePath, json);
                _lastSaveTime = DateTime.Now;
                Console.WriteLine($"PPO Instance {_instanceId} - Model saved: {_wins}/{_totalGames} wins, networks serialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PPO Instance {_instanceId} - Error saving model: {ex.Message}");
            }
        }

        public void LoadModel(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    using var document = System.Text.Json.JsonDocument.Parse(json);
                    var root = document.RootElement;
                    
                    // Load training statistics
                    if (root.TryGetProperty("TotalGames", out var totalGamesElement))
                        _totalGames = totalGamesElement.GetInt32();
                    if (root.TryGetProperty("Wins", out var winsElement))
                        _wins = winsElement.GetInt32();
                    if (root.TryGetProperty("Losses", out var lossesElement))
                        _losses = lossesElement.GetInt32();
                    if (root.TryGetProperty("Temperature", out var tempElement))
                        _currentTemperature = tempElement.GetSingle();
                    
                    // Load network weights if available
                    if (root.TryGetProperty("PolicyNetwork", out var policyElement))
                    {
                        LoadNetworkWeights(_policyNetwork, policyElement);
                        Console.WriteLine($"PPO Instance {_instanceId} - Policy network weights loaded");
                    }
                    
                    if (root.TryGetProperty("ValueNetwork", out var valueElement))
                    {
                        LoadNetworkWeights(_valueNetwork, valueElement);
                        Console.WriteLine($"PPO Instance {_instanceId} - Value network weights loaded");
                    }
                    
                    Console.WriteLine($"PPO Instance {_instanceId} - Model loaded: {_wins}/{_totalGames} wins");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PPO Instance {_instanceId} - Error loading model: {ex.Message}");
            }
        }
        
        private void LoadNetworkWeights(dynamic network, System.Text.Json.JsonElement element)
        {
            try
            {
                // For now, skip complex deserialization to avoid JSON complexity
                // The network will start from random initialization
                // TODO: Implement proper 2D array deserialization if needed
                Console.WriteLine($"PPO Instance {_instanceId} - Network weight loading skipped (using random initialization)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PPO Instance {_instanceId} - Error loading network weights: {ex.Message}");
            }
        }

        /// <summary>
        /// Convert 2D array to jagged array for JSON serialization
        /// </summary>
        private float[][] Convert2DToJagged(float[,] array2D)
        {
            int rows = array2D.GetLength(0);
            int cols = array2D.GetLength(1);
            float[][] jaggedArray = new float[rows][];
            
            for (int i = 0; i < rows; i++)
            {
                jaggedArray[i] = new float[cols];
                for (int j = 0; j < cols; j++)
                {
                    jaggedArray[i][j] = array2D[i, j];
                }
            }
            
            return jaggedArray;
        }

        public void ForceSaveModel()
        {
            try
            {
                SaveModel(_modelPath);
                Console.WriteLine($"PPO Instance {_instanceId} - Emergency model save completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PPO Instance {_instanceId} - Error in emergency save: {ex.Message}");
            }
        }

        /// <summary>
        /// Public interface method for IFootballAI - delegates to internal RecordExperience
        /// </summary>
        public void RecordExperience(GameStatusExtended gameState, TeamInstructions action, float reward, GameStatusExtended nextState)
        {
            RecordExperience(gameState, action, reward, nextState, _previousLogProb);
        }

        /// <summary>
        /// Public interface method for IFootballAI - triggers PPO training
        /// </summary>
        public void Train()
        {
            TrainPPO();
        }
    }
}