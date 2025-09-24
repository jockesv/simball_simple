using FootballInstructor.Domain.Model;
using System.Collections.Concurrent;
using FootballInstructor.Configuration;

namespace FootballInstructor.Domain.AI
{
    public class ReinforcementLearningAI : IFootballAI
    {
        private SimpleNeuralNetwork _network;
        private readonly GameStateEncoder _encoder;
        private readonly RewardCalculator _rewardCalculator;
        private readonly ConcurrentQueue<GameExperience> _experienceBuffer;
        private readonly Random _random;
        private readonly string _modelPath;
        private readonly int _instanceId;
        private readonly AISettings _settings;
        private readonly PlayerService _teacher; // heuristic teacher for imitation
        
        // Training parameters
        private const int MAX_BUFFER_SIZE = 10000;
        private const int BATCH_SIZE = 32;
        private const float EXPLORATION_RATE = 0.1f;
        private const float EXPLORATION_DECAY = 0.995f;
        private float _currentExplorationRate = EXPLORATION_RATE;
        
        // Game state tracking
        private GameStatusExtended? _previousGameState;
        private TeamInstructions? _previousAction;
        private int _stepCount = 0;
        
        // Performance tracking
        private int _totalGames = 0;
        private int _wins = 0;
        private int _losses = 0;
        private List<float> _recentRewards = new();
        
        // Model saving
        private DateTime _lastSaveTime = DateTime.Now;
        private const int SAVE_INTERVAL_GAMES = 5; // Save every 5 games (more frequent)
        private const int SAVE_INTERVAL_MINUTES = 2; // Or every 2 minutes (more frequent)

        public ReinforcementLearningAI(int? seed = null, int instanceId = 1, AISettings? settings = null)
        {
            _instanceId = instanceId;
            _settings = settings ?? new AISettings();
            _encoder = new GameStateEncoder();
            _rewardCalculator = new RewardCalculator();
            _experienceBuffer = new ConcurrentQueue<GameExperience>();
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _teacher = new PlayerService(); // heuristic teacher for imitation learning
            
            // Set up model path - each instance has its own model file
            _modelPath = Path.Combine("models", $"rl_model_instance_{_instanceId}.json");
            
            // Try to load existing model first
            var (loadedNetwork, totalGames, wins, losses, explorationRate) = ModelSerializer.LoadModel(
                _modelPath,
                GameStateEncoder.StateSize,
                256,
                128,
                GameStateEncoder.ActionSize
            );
            
            if (loadedNetwork != null)
            {
                _network = loadedNetwork;
                _totalGames = totalGames;
                _wins = wins;
                _losses = losses;
                _currentExplorationRate = _settings.ExplorationRate > 0 ? _settings.ExplorationRate : explorationRate;
                Console.WriteLine($"Loaded existing model for instance {_instanceId}: {_wins}/{_totalGames} wins, exploration: {_currentExplorationRate:F3}");
            }
            else
            {
                // Create new network
                _network = new SimpleNeuralNetwork(
                    inputSize: GameStateEncoder.StateSize,
                    hiddenSize1: 256,
                    hiddenSize2: 128,
                    outputSize: GameStateEncoder.ActionSize,
                    learningRate: _settings.ImitationLearningEnabled ? 0.01f : 0.001f, // Moderate learning rate for imitation
                    momentum: 0.9f,
                    seed: seed
                );
                _currentExplorationRate = _settings.ExplorationRate;
                Console.WriteLine($"Created new model for instance {_instanceId} (exploration={_currentExplorationRate:F3})");
            }
            
            Console.WriteLine($"ReinforcementLearningAI instance {_instanceId} initialized");
            Console.WriteLine($"[DEBUG] Imitation learning enabled: {_settings.ImitationLearningEnabled}, steps: {_settings.ImitationEveryNSteps}, weight: {_settings.ImitationWeight}");
        }

        public TeamInstructions GetTeamInstructions(GameStatusExtended gameStatus)
        {
            // Handle game resets (new game or kickoff)
            if (gameStatus.KickoffSinceLastUpdate || _previousGameState == null)
            {
                HandleGameReset(gameStatus);
            }
            
            // Calculate reward from previous action if we have previous state
            if (_previousGameState != null && _previousAction != null)
            {
                var reward = _rewardCalculator.CalculateReward(_previousGameState, _previousAction, gameStatus);
                RecordExperience(_previousGameState, _previousAction, reward, gameStatus);
                
                _recentRewards.Add(reward);
                if (_recentRewards.Count > 100)
                    _recentRewards.RemoveAt(0);
                
                // Check for game end
                if (gameStatus.YouScored || gameStatus.OpponentScored)
                {
                    HandleGameEnd(gameStatus);
                }
            }
            
            // Get action (with exploration)
            var action = GetAction(gameStatus);
            
            // Store current state and action for next iteration
            _previousGameState = CloneGameState(gameStatus);
            _previousAction = action;
            _stepCount++;
            
            // Train periodically
            if (_stepCount % 10 == 0 && _experienceBuffer.Count >= BATCH_SIZE)
            {
                Train();
            }
            // Perform periodic imitation learning update towards heuristic teacher
            if (_settings.ImitationLearningEnabled && _stepCount % Math.Max(1, _settings.ImitationEveryNSteps) == 0)
            {
                try
                {
                    PerformImitationUpdate(gameStatus);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[IMIT] Error during imitation update: {ex.Message}");
                }
            }
            
            return action;
        }

        private TeamInstructions GetAction(GameStatusExtended gameStatus)
        {
            // If imitation learning is enabled with high weight, blend network and teacher actions
            if (_settings.ImitationLearningEnabled && _settings.ImitationWeight > 0.5f)
            {
                var teacherAction = _teacher.Update(gameStatus);
                
                // During early training, use mostly teacher actions with small network influence
                var blendRatio = Math.Min(_settings.ImitationWeight, 0.95f); // Cap at 95% teacher
                
                if (_random.NextDouble() < blendRatio)
                {
                    // Use teacher action with small random variation to help network learn
                    var action = AddSmallVariation(teacherAction, 0.1f);
                    if (_settings.EnableLogging && _stepCount % 20 == 0)
                    {
                        Console.WriteLine($"[TEACHER] P1 move: ({action.P1Instructions.MoveToX},{action.P1Instructions.MoveToY}) vel:{action.P1Instructions.MoveVelocity}");
                    }
                    return action;
                }
            }
            
            var state = _encoder.EncodeGameState(gameStatus);
            var networkOutput = _network.Forward(state);
            var networkAction = _encoder.DecodeAction(networkOutput);
            
            // Epsilon-greedy exploration
            if (_random.NextDouble() < _currentExplorationRate)
            {
                // Random exploration
                networkAction = GetRandomAction();
                if (_settings.EnableLogging && _stepCount % 20 == 0)
                {
                    Console.WriteLine($"[EXPLORE] P1 move: ({networkAction.P1Instructions.MoveToX},{networkAction.P1Instructions.MoveToY}) vel:{networkAction.P1Instructions.MoveVelocity}");
                }
            }
            else
            {
                if (_settings.EnableLogging && _stepCount % 20 == 0)
                {
                    Console.WriteLine($"[NETWORK] P1 move: ({networkAction.P1Instructions.MoveToX},{networkAction.P1Instructions.MoveToY}) vel:{networkAction.P1Instructions.MoveVelocity}");
                    Console.WriteLine($"[DEBUG] Raw outputs: [{string.Join(",", networkOutput.Take(6).Select(x => x.ToString("F3")))}...]");
                }
            }
            
            return networkAction;
        }

        private TeamInstructions GetRandomAction()
        {
            return new TeamInstructions
            {
                P1Instructions = GetRandomPlayerInstructions(),
                P2Instructions = GetRandomPlayerInstructions(),
                P3Instructions = GetRandomPlayerInstructions(),
                P4Instructions = GetRandomPlayerInstructions()
            };
        }

        private PlayerInstructions GetRandomPlayerInstructions()
        {
            // Avoid sampling moves directly into corners during exploration to prevent corner bias
            int SafeX() => _random.Next(1000, 11001); // 1000..11000
            int SafeY() => _random.Next(1000, 8001);  // 1000..8000

            return new PlayerInstructions
            {
                MoveToX = SafeX(),
                MoveToY = SafeY(),
                MoveVelocity = _random.Next(20, 101), // avoid standing still during exploration
                BallTargetX = SafeX(),
                BallTargetY = SafeY(),
                BallVelocity = _random.Next(0, 101)
            };
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
                MoveToX = Math.Clamp((int)(instruction.MoveToX * (1 + noise() * 0.1)), 0, 12000),
                MoveToY = Math.Clamp((int)(instruction.MoveToY * (1 + noise() * 0.1)), 0, 9000),
                MoveVelocity = Math.Clamp((int)(instruction.MoveVelocity * (1 + noise() * 0.1)), 0, 100),
                BallTargetX = Math.Clamp((int)(instruction.BallTargetX * (1 + noise() * 0.1)), 0, 12000),
                BallTargetY = Math.Clamp((int)(instruction.BallTargetY * (1 + noise() * 0.1)), 0, 9000),
                BallVelocity = Math.Clamp((int)(instruction.BallVelocity * (1 + noise() * 0.1)), 0, 100)
            };
        }

        private TeamInstructions AddActionNoise(TeamInstructions action, float noiseFactor)
        {
            return new TeamInstructions
            {
                P1Instructions = AddPlayerInstructionNoise(action.P1Instructions, noiseFactor),
                P2Instructions = AddPlayerInstructionNoise(action.P2Instructions, noiseFactor),
                P3Instructions = AddPlayerInstructionNoise(action.P3Instructions, noiseFactor),
                P4Instructions = AddPlayerInstructionNoise(action.P4Instructions, noiseFactor)
            };
        }

        private PlayerInstructions AddPlayerInstructionNoise(PlayerInstructions instruction, float noiseFactor)
        {
            var noise = () => (float)(_random.NextDouble() * 2 - 1) * noiseFactor;
            
            return new PlayerInstructions
            {
                MoveToX = Math.Clamp((int)(instruction.MoveToX * (1 + noise())), 0, 12000),
                MoveToY = Math.Clamp((int)(instruction.MoveToY * (1 + noise())), 0, 9000),
                MoveVelocity = Math.Clamp((int)(instruction.MoveVelocity * (1 + noise())), 0, 100),
                BallTargetX = Math.Clamp((int)(instruction.BallTargetX * (1 + noise())), 0, 12000),
                BallTargetY = Math.Clamp((int)(instruction.BallTargetY * (1 + noise())), 0, 9000),
                BallVelocity = Math.Clamp((int)(instruction.BallVelocity * (1 + noise())), 0, 100)
            };
        }

        public void RecordExperience(GameStatusExtended gameState, TeamInstructions action, float reward, GameStatusExtended nextState)
        {
            var experience = new GameExperience
            {
                State = _encoder.EncodeGameState(gameState),
                Action = _encoder.EncodeAction(action),
                Reward = reward,
                NextState = _encoder.EncodeGameState(nextState),
                IsTerminal = nextState.YouScored || nextState.OpponentScored,
                Timestamp = DateTime.Now
            };

            _experienceBuffer.Enqueue(experience);

            // Keep buffer size manageable
            while (_experienceBuffer.Count > MAX_BUFFER_SIZE)
            {
                _experienceBuffer.TryDequeue(out _);
            }
        }

        public void Train()
        {
            if (_experienceBuffer.Count < BATCH_SIZE) return;

            // Sample random batch
            var experiences = SampleBatch(BATCH_SIZE);
            
            // Filter to only learn from experiences that had a signal
            // With minimal rewards, these are typically goal events (+/-100)
            var informative = experiences.Where(e => Math.Abs(e.Reward) > 1e-6).ToList();
            if (informative.Count == 0)
            {
                // Nothing to learn from this batch
                return;
            }
            
            // Train on batch
            foreach (var experience in informative)
            {
                // Policy-like update: move network output towards taken action if reward>0, away if reward<0
                var currentOutput = _network.Forward(experience.State);
                var targetAction = currentOutput.ToArray();

                // Normalize reward to [-1,1] assuming +/-100 range for goals
                var r = Math.Clamp(experience.Reward / 100f, -1f, 1f);
                var alpha = 0.5f * Math.Abs(r); // step size proportional to reward magnitude
                var direction = Math.Sign(r);   // +1 towards action, -1 away from action

                for (int i = 0; i < targetAction.Length; i++)
                {
                    var desired = experience.Action[i]; // encoded action in [0,1]
                    var curr = currentOutput[i];
                    var delta = desired - curr;
                    targetAction[i] = Math.Clamp(curr + direction * alpha * delta, 0f, 1f);
                }

                _network.BackwardAndUpdate(experience.State, targetAction);
            }

            // Decay exploration rate
            _currentExplorationRate = Math.Max(0.01f, _currentExplorationRate * EXPLORATION_DECAY);
            
            if (_stepCount % 100 == 0)
            {
                LogTrainingProgress();
            }

            // Lightweight train log when we actually applied updates
            Console.WriteLine($"[TRAIN] Applied updates from {informative.Count} informative experiences. Exploration: {_currentExplorationRate:F3}");
        }

        private void PerformImitationUpdate(GameStatusExtended gameStatus)
        {
            // Use the teacher to get target actions for the current state
            var teacherAction = _teacher.Update(gameStatus);
            var state = _encoder.EncodeGameState(gameStatus);
            var teacherEncoded = _encoder.EncodeAction(teacherAction);

            // Use a more conservative approach: direct teacher supervision with reduced learning rate
            var currentOutput = _network.Forward(state);
            
            // For imitation learning, train directly towards teacher action but with reduced intensity
            var target = teacherEncoded.ToArray();
            
            // Apply a dampening factor to prevent network explosion
            var dampening = 0.1f; // Much more conservative
            for (int i = 0; i < target.Length; i++)
            {
                var curr = currentOutput[i];
                target[i] = curr + dampening * (target[i] - curr);
                target[i] = Math.Clamp(target[i], 0.01f, 0.99f); // Avoid extreme values
            }

            _network.BackwardAndUpdate(state, target);
            
            // Force save model every 100 imitation steps to ensure learning is preserved
            if (_stepCount % 100 == 0)
            {
                SaveModel(_modelPath);
                Console.WriteLine($"[IMIT] Auto-saved model after {_stepCount} steps");
            }
            
            if (_settings.EnableLogging && _stepCount % 20 == 0) // Log less frequently to avoid spam
            {
                var newOutput = _network.Forward(state);
                var newAction = _encoder.DecodeAction(newOutput);
                Console.WriteLine($"[IMIT] Teacher P1: ({teacherAction.P1Instructions.MoveToX},{teacherAction.P1Instructions.MoveToY}) vel:{teacherAction.P1Instructions.MoveVelocity}");
                Console.WriteLine($"[IMIT] Network P1: ({newAction.P1Instructions.MoveToX},{newAction.P1Instructions.MoveToY}) vel:{newAction.P1Instructions.MoveVelocity}");
                Console.WriteLine($"[IMIT] Applied conservative update (dampening=0.1)");
            }
        }

        private List<GameExperience> SampleBatch(int batchSize)
        {
            var allExperiences = _experienceBuffer.ToArray();
            var batch = new List<GameExperience>();
            
            for (int i = 0; i < Math.Min(batchSize, allExperiences.Length); i++)
            {
                var randomIndex = _random.Next(allExperiences.Length);
                batch.Add(allExperiences[randomIndex]);
            }
            
            return batch;
        }

        private void HandleGameReset(GameStatusExtended gameStatus)
        {
            _rewardCalculator.Reset();
            _previousGameState = null;
            _previousAction = null;
            
            Console.WriteLine($"Game reset detected. Total games: {_totalGames}, Win rate: {GetWinRate():F2}%");
        }

        private void HandleGameEnd(GameStatusExtended gameStatus)
        {
            _totalGames++;
            
            if (gameStatus.YouScored)
                _wins++;
            else if (gameStatus.OpponentScored)
                _losses++;
            
            var avgReward = _recentRewards.Count > 0 ? _recentRewards.Average() : 0;
            Console.WriteLine($"Instance {_instanceId} - Game ended. Score: {gameStatus.YourScore}-{gameStatus.OpponentScore}, " +
                            $"Avg reward: {avgReward:F2}, Win rate: {GetWinRate():F2}%, " +
                            $"Exploration: {_currentExplorationRate:F3}");
            
            // Auto-save model periodically
            var shouldSave = _totalGames % SAVE_INTERVAL_GAMES == 0 || 
                           (DateTime.Now - _lastSaveTime).TotalMinutes >= SAVE_INTERVAL_MINUTES;
            
            if (shouldSave)
            {
                SaveModel(_modelPath);
            }
        }

        private double GetWinRate()
        {
            return _totalGames > 0 ? (_wins * 100.0 / _totalGames) : 0;
        }

        private void LogTrainingProgress()
        {
            var avgReward = _recentRewards.Count > 0 ? _recentRewards.Average() : 0;
            Console.WriteLine($"Step {_stepCount}: Avg reward: {avgReward:F2}, " +
                            $"Buffer size: {_experienceBuffer.Count}, " +
                            $"Exploration: {_currentExplorationRate:F3}, " +
                            $"Win rate: {GetWinRate():F2}%");
        }

        private GameStatusExtended CloneGameState(GameStatusExtended original)
        {
            // Simple clone by creating new instance and copying values
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
                ModelSerializer.SaveModel(_network, filePath, _totalGames, _wins, _losses, _currentExplorationRate);
                _lastSaveTime = DateTime.Now;
                Console.WriteLine($"Instance {_instanceId} - Model saved: {_wins}/{_totalGames} wins");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Instance {_instanceId} - Error saving model: {ex.Message}");
            }
        }

        /// <summary>
        /// Force save model immediately, ignoring intervals
        /// </summary>
        public void ForceSaveModel()
        {
            try
            {
                SaveModel(_modelPath);
                Console.WriteLine($"Instance {_instanceId} - Emergency model save completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Instance {_instanceId} - Error in emergency save: {ex.Message}");
            }
        }

        public void LoadModel(string filePath)
        {
            try
            {
                var (loadedNetwork, totalGames, wins, losses, explorationRate) = ModelSerializer.LoadModel(
                    filePath,
                    GameStateEncoder.StateSize,
                    256,
                    128,
                    GameStateEncoder.ActionSize
                );
                
                if (loadedNetwork != null)
                {
                    _network = loadedNetwork;
                    _totalGames = totalGames;
                    _wins = wins;
                    _losses = losses;
                    _currentExplorationRate = explorationRate;
                    Console.WriteLine($"Instance {_instanceId} - Model loaded: {_wins}/{_totalGames} wins");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Instance {_instanceId} - Error loading model: {ex.Message}");
            }
        }
    }
}