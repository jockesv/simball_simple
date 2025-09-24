#!/bin/bash

# Reset AI training - removes old models that learned bad behavior
echo "🧹 Resetting AI training..."

# Stop any running instances
echo "Stopping any running AI instances..."
pkill -f "dotnet run --urls"
sleep 2

# Remove old models
echo "Removing old model files..."
if [ -d "models" ]; then
    rm -f models/rl_model_instance_*.json
    echo "✓ Removed old model files"
else
    echo "• No models directory found"
fi

# Build the project with updated reward system
echo "Building project with improved reward system..."
dotnet build

if [ $? -eq 0 ]; then
    echo "✅ Build successful!"
    echo ""
    echo "🆕 AI will now learn from scratch with the new reward system that:"
    echo "   • Severely punishes corner hiding (-5.0 penalty)"
    echo "   • Heavily penalizes passive play (-5.0 penalty)"
    echo "   • Strongly rewards ball engagement (+3.0 reward)"
    echo "   • Prevents kickoff formation behavior"
    echo ""
    echo "🚀 Ready to start fresh training with: ./start_training.sh"
    echo ""
    echo "📊 The new reward system should eliminate:"
    echo "   ❌ Players running to corners after kickoff"
    echo "   ❌ Static rectangular formations"
    echo "   ❌ Passive behavior avoiding the ball"
    echo ""
    echo "✅ And encourage:"
    echo "   ✓ Active ball pursuit"
    echo "   ✓ Dynamic movement"
    echo "   ✓ Competitive gameplay"
else
    echo "❌ Build failed. Check for errors above."
    exit 1
fi