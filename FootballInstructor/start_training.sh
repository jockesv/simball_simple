#!/bin/bash

# Start multiple AI instances for self-play training

echo "Starting RL AI training setup..."

# Create models directory if it doesn't exist
mkdir -p models

# Function to cleanup background processes
cleanup() {
    echo ""
    echo "🛑 Stopping training and saving models..."
    
    # Force save models before killing processes
    echo "💾 Forcing model saves..."
    
    # Try to save Team 1 model (port 5004)
    curl -s -X GET http://localhost:5004/Player/force-save-model > /dev/null 2>&1 && echo "✓ Team 1 model saved" || echo "• Team 1 not responding"
    
    # Try to save Team 2 model (port 5002)
    curl -s -X GET http://localhost:5002/Player/force-save-model > /dev/null 2>&1 && echo "✓ Team 2 model saved" || echo "• Team 2 not responding"
    
    # Try to save Heuristic (port 5003) - won't have model but for completeness
    curl -s -X GET http://localhost:5003/Player/force-save-model > /dev/null 2>&1 && echo "✓ Team 3 checked" || echo "• Team 3 not responding"
    
    # Give processes a moment to finish saving
    sleep 2
    
    echo "🔪 Terminating AI processes..."
    jobs -p | xargs -r kill -TERM 2>/dev/null
    
    # Wait a bit for graceful shutdown
    sleep 3
    
    # Force kill if still running
    jobs -p | xargs -r kill -KILL 2>/dev/null
    
    echo ""
    echo "🎯 Training session ended. Checking saved models..."
    if [ -f "models/rl_model_instance_4.json" ]; then
        echo "  ✅ models/rl_model_instance_4.json (Team 1 on port 5004)"
    else
        echo "  ❌ models/rl_model_instance_4.json (Team 1) - not saved"
    fi
    
    if [ -f "models/rl_model_instance_2.json" ]; then
        echo "  ✅ models/rl_model_instance_2.json (Team 2 on port 5002)"
    else
        echo "  ❌ models/rl_model_instance_2.json (Team 2) - not saved"
    fi
    
    echo ""
    echo "🔄 Next time you run this script, the AIs will continue from their saved state!"
    exit 0
}

# Set trap to cleanup on script exit
trap cleanup SIGINT SIGTERM

# Build the project first
echo "Building project..."
dotnet build

if [ $? -ne 0 ]; then
    echo "Build failed. Exiting."
    exit 1
fi

echo ""
echo "Checking for existing models..."
if [ -f "models/rl_model_instance_1.json" ]; then
    echo "✓ Found existing model for Team 1 - will continue training"
else
    echo "• No existing model for Team 1 - starting fresh"
fi

if [ -f "models/rl_model_instance_2.json" ]; then
    echo "✓ Found existing model for Team 2 - will continue training"
else
    echo "• No existing model for Team 2 - starting fresh"
fi

echo ""
echo "Starting AI instances..."

# Start RL AI Team 1 on port 5004
echo "Starting RL AI Team 1 on port 5004..."
dotnet run --urls=http://localhost:5004 &
PID1=$!

sleep 3

# Start RL AI Team 2 on port 5002  
echo "Starting RL AI Team 2 on port 5002..."
dotnet run --urls=http://localhost:5002 &
PID2=$!

sleep 3

# Start Heuristic AI on port 5003 (optional, for comparison)
echo "Starting Heuristic AI on port 5003..."
AISettings__AIType="Heuristic" dotnet run --urls=http://localhost:5003 &
PID3=$!

sleep 2

echo ""
echo "🤖 All AI instances started successfully!"
echo ""
echo "📊 Configure these URLs in the football simulator:"
echo "   RL AI Team 1: http://localhost:5004"
echo "   RL AI Team 2: http://localhost:5002" 
echo "   Heuristic AI: http://localhost:5003"
echo ""
echo "🧠 The RL teams will:"
echo "   • Learn by playing against each other"
echo "   • Save their progress every 10 games or 5 minutes"
echo "   • Continue improving from their last saved state"
echo ""
echo "📈 Watch the console logs to monitor training progress!"
echo "   Look for: Win rates, exploration rates, and average rewards"
echo ""
echo "Press Ctrl+C to stop all instances and save models."

# Wait for all background processes
wait