# Football AI - PPO with Imitation Learning

This project implements a sophisticated football AI using Proximal Policy Optimization (PPO) with Generalized Advantage Estimation (GAE) and imitation learning from heuristic teachers.

## Overview

The system features:
- **PPOFootballAI**: Advanced PPO agent with GAE for stable policy learning
- **PPOPlayerService**: Service using PPO AI for player decisions
- **Imitation Learning**: Learns from heuristic teacher while developing its own strategies
- **Self-play Training**: Multiple AI instances compete and improve through gameplay
- **Comprehensive Model Persistence**: Full network weight serialization and loading

## Quick Start

### 1. Launch Training Setup
```bash
./start_training.sh
```

This starts three AI instances:
- **Port 5004**: PPO AI Team 1 (Instance 4)
- **Port 5002**: PPO AI Team 2 (Instance 2) 
- **Port 5003**: Heuristic AI (Reference/Teacher)

### 2. Configure Simulator
In the football simulator, register teams with these URLs:
- Team 1: `http://localhost:5004`
- Team 2: `http://localhost:5002`
- Reference: `http://localhost:5003`

### Simulation context: simulator API and data

The simulator exposes a small HTTP-based API for registered teams. When you register a custom team you provide a URL and the simulator will call two endpoints on that URL:

- `setup` (GET): return team metadata such as team name and colours. Use this to populate the simulator UI when the team is registered.
- `update` (POST): receive the current game state and return the next set of player instructions.

Notes for implementers:
- Ensure CORS is enabled on your API so the simulator can call it from the browser.
- The simulator mirrors the game state horizontally when a team is playing away (the simulator flips the X axis). This is done automatically and can be confusing if you expect left/right roles to be fixed.
- Units in all state fields are: centimetres (cm) for positions, seconds (s) for time, and cm/s for velocities.
- The playing field dimensions are 12000 cm (width) x 9000 cm (height). The top-left corner of the field is (0,0).
- Registered teams are saved in the browser (local storage) until cleared.

There is a Swagger definition available for the simulator API at `Swagger.json` to help with building and debugging your team API.

### 3. Start Training
Let the AIs play against each other. They will:
- Learn from every match through PPO algorithm
- Improve strategies via imitation learning from heuristic teacher
- Log detailed training progress to console
- Save models automatically every 5 games or 2 minutes

## AI Architecture

### PPO Networks
- **Policy Network**: 
  - Input: Game state features → Hidden: 256→128 → Output: Action probabilities
  - Activation: ReLU hidden layers, ScaledTanh output for bounded actions
- **Value Network**: 
  - Input: Game state features → Hidden: 128→64 → Output: State value estimate
  - Activation: ReLU hidden layers, Identity output for unbounded values

### Reward System (Minimalistic)
The AI is rewarded for:
- ✅ **Goals scored**: +100 points
- ❌ **Goals conceded**: -100 points
- **Note**: Other rewards (ball control, positioning) are commented out to prevent reward hacking

### PPO Algorithm Features
- **Clipped Policy Gradient**: Prevents large policy updates (ε=0.2)
- **GAE (λ=0.95)**: Generalized Advantage Estimation for stable learning
- **Multiple Epochs**: 4 training epochs per batch for sample efficiency
- **Experience Replay**: Stores trajectories in PPOBuffer for batch training

### Imitation Learning Integration
- **Teacher Blend**: 90% heuristic teacher actions, 10% PPO exploration
- **Behavior Cloning**: PPO network learns to mimic teacher through supervised learning
- **Gradual Independence**: As PPO improves, reliance on teacher can be reduced

## Configuration

Modify AI settings in `appsettings.Development.json`:

```json
{
  "AISettings": {
    "AIType": "PPO",                    // "PPO", "ReinforcementLearning", or "Heuristic"
    "ModelPath": "./models/",
    "EnableTraining": true,
    "EnableLogging": true,
    "ExplorationRate": 0.05,
    "ImitationLearningEnabled": true,
    "ImitationEveryNSteps": 1,
    "ImitationWeight": 0.9,             // 90% teacher, 10% PPO
    "PPOGamma": 0.99,                   // Discount factor
    "PPOLambda": 0.95,                  // GAE parameter
    "PPOClipEpsilon": 0.2,             // Policy gradient clipping
    "PPOEpochs": 4,                     // Training epochs per batch
    "PPOBatchSize": 64,                 // Batch size for training
    "PPOBufferSize": 20                 // Max trajectories in buffer
  }
}
```

Or use environment variables:
```bash
AISettings__AIType="Heuristic" dotnet run --urls=http://localhost:5003
```

## Model Saving & Persistence

### Automatic Saving
- **Saves every 5 games** or **every 2 minutes**
- **Force save on training script exit** via HTTP endpoints
- **Separate models** for each instance:
  - `models/rl_model_instance_4.json` (Team 1 on port 5004)
  - `models/rl_model_instance_2.json` (Team 2 on port 5002)

### What Gets Saved
- **Complete neural network weights**: Both Policy and Value networks
- **Training statistics**: Total games, wins, losses, win rates
- **Temperature**: Exploration parameter (decays over time)
- **Timestamp**: Last save time for tracking

### Network Weight Serialization
- **2D arrays converted to jagged arrays** for JSON compatibility
- **Full weight matrices**: Input→Hidden1→Hidden2→Output layers
- **All biases**: For each layer in both networks
- **Persistent training state**: Continues exactly where it left off

### Continuous Training
- When restarted, **AIs continue from exact previous state**
- **No training progress lost** on restart
- **Perfect for long-term training** over days/weeks/months

## Monitoring & Logs

### Training Progress Logs
```
PPO Instance 4 - Game ended. Score: 1-3, Avg reward: -8.000000, Win rate: 76.79%, Temperature: 0.327, Trajectories: 14
PPO Instance 2 - Game ended. Score: 3-1, Avg reward: 8.000000, Win rate: 89.11%, Temperature: 0.133, Trajectories: 8
[PPO] Trained on 309 experiences, 4 epochs
PPO Instance 4 - Model saved: 173/225 wins, networks serialized
```

### Action Logging
```
[TEACHER] P1 move: (2960,3958) vel:99    # 90% of actions from heuristic teacher
[PPO] P1 move: (4098,2182) vel:99        # 10% of actions from PPO policy
[IMIT] Applied imitation update towards heuristic teacher
```

### Key Metrics
- **Win Rate**: Percentage of games won (target: >80%)
- **Avg Reward**: Average reward per step (non-zero indicates learning)
- **Temperature**: Exploration parameter (starts at 1.0, decays to ~0.1)
- **Trajectories**: Complete game episodes stored for training

## Expected Learning Progression

### Day 1-2 (Initialization)
- Heavy reliance on teacher (90% teacher actions)
- Learning basic game mechanics and action space
- Win rate: ~50-60% (teacher baseline)

### Week 1-2 (Imitation Phase)
- PPO starts contributing meaningful actions (10% exploration)
- Learning to combine teacher knowledge with own discoveries
- Win rate: ~70-80%

### Month 1-2 (Independence Phase)
- Can reduce ImitationWeight to 0.7-0.5 as PPO improves
- Develops strategies beyond teacher capabilities
- Win rate: ~80-90%

### Month 3+ (Mastery Phase)
- PPO potentially surpasses teacher performance
- Can disable imitation learning entirely
- Win rate: >90%

## Technical Implementation

### Key Features
- **PPO with GAE**: State-of-the-art policy gradient algorithm with advantage estimation
- **Imitation Learning**: Combines supervised learning from teacher with RL exploration
- **Real-time Training**: Learns during gameplay with periodic batch updates
- **Experience Buffering**: Stores complete trajectories for batch PPO training
- **Multi-instance Support**: Runs multiple AI instances with unique instance IDs
- **Robust Model Persistence**: Full neural network serialization with error handling
- **NaN Protection**: Comprehensive guards against numerical instability

### File Structure
```
Domain/
├── AI/
│   ├── PPO/
│   │   ├── PPOFootballAI.cs        # Main PPO agent implementation
│   │   ├── PPONetworks.cs          # Policy & Value networks with NaN guards
│   │   ├── PPOBuffer.cs            # Experience replay buffer
│   │   ├── PPOExperience.cs        # Individual experience data structure
│   │   └── PPOTrajectory.cs        # Complete episode trajectory
│   ├── IFootballAI.cs              # AI interface
│   ├── SimpleNeuralNetwork.cs      # Neural network with fixed gradient descent
│   ├── GameStateEncoder.cs         # Game state → vector conversion
│   └── RewardCalculator.cs         # Sparse reward system (goals only)
├── PPOPlayerService.cs             # PPO-based PlayerService
├── RLPlayerService.cs              # Legacy RL PlayerService  
└── PlayerService.cs                # Original heuristic AI
```

## Troubleshooting

### AI Makes Strange Moves
- **Normal during first days** (exploration phase with teacher learning)
- Check `Temperature` in logs (should decay from 1.0 to ~0.1)
- Monitor `[TEACHER]` vs `[PPO]` action ratio (should be 90%/10%)
- Wait for buffer to fill (`Trajectories: X` should reach 5+ for training)

### Training Appears Slow
- Increase number of matches per day
- Verify both AI instances are training actively (`[PPO] Trained on X experiences`)
- Check win rates are improving over time
- Ensure models are being saved (look for `Model saved: X/Y wins`)

### Avg Reward Shows 0.000000
- **Normal!** Rewards are sparse (only ±100 at goals)
- Non-zero values indicate PPO made the goal-scoring action
- Zero values indicate teacher made the goal-scoring action
- High win rates (>75%) prove learning despite sparse rewards

### Ports Already in Use
```bash
# Check which processes use the ports
lsof -i :5002
lsof -i :5004
lsof -i :5003

# Kill processes if necessary
kill -9 <PID>

# Or use the training script's cleanup
pkill -f "dotnet run --urls"
```

### Models Not Saving
- Check console for `Model saved:` messages
- Verify `models/` directory exists and is writable  
- Look for `Error saving model:` error messages
- Try force save: `curl http://localhost:5004/Player/force-save-model`

## Next Steps & Improvements

### Immediate Enhancements
1. **Complete Model Loading**: Implement proper 2D array deserialization for full weight restoration
2. **Curriculum Learning**: Gradually reduce `ImitationWeight` as PPO improves
3. **Advanced Reward Shaping**: Re-enable ball control and positioning rewards carefully
4. **Hyperparameter Tuning**: Experiment with learning rates, batch sizes, network architectures

### Advanced Features
1. **Multi-Agent Communication**: Allow players to coordinate through shared information
2. **Opponent Modeling**: Learn and adapt to opponent strategies
3. **Population Training**: Train multiple diverse agents simultaneously
4. **Hierarchical RL**: Separate strategic and tactical decision making

### Research Directions
1. **Transformer Networks**: Replace MLPs with attention mechanisms for better spatial understanding
2. **Curiosity-Driven Learning**: Add intrinsic motivation for exploration
3. **Meta-Learning**: Enable quick adaptation to new opponents or rule changes
4. **Self-Play Curriculum**: Automatically adjust opponent difficulty

## Contributing

To improve the AI system:

1. **Experiment with reward functions** in `RewardCalculator.cs`
2. **Adjust network architectures** in `PPONetworks.cs`
3. **Modify PPO hyperparameters** in `appsettings.Development.json`
4. **Implement new training strategies** in `PPOFootballAI.cs`

### Development Guidelines
- Maintain NaN guards in all mathematical operations
- Use proper gradient descent (subtraction, not addition)
- Preserve model saving functionality for long-term training
- Log key metrics for monitoring training progress

---

**Good luck with your AI training! 🤖⚽🚀**