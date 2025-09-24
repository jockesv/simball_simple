#!/bin/bash

# Test script for the RL AI API

echo "Testing RL AI API..."

# Start the application in background
cd FootballInstructor
dotnet run --urls=http://localhost:5001 &
SERVER_PID=$!

# Wait a bit for server to start
echo "Waiting for server to start..."
sleep 5

# Test setup endpoint
echo "Testing setup endpoint..."
curl -X GET http://localhost:5001/Player/setup

echo ""
echo ""

# Test with a simple game state for update endpoint
echo "Testing update endpoint with sample data..."
curl -X POST http://localhost:5001/Player/update \
  -H "Content-Type: application/json" \
  -d '{
    "correlationId": "test-123",
    "youScored": false,
    "opponentScored": false,
    "kickoffSinceLastUpdate": true,
    "timeLeftMS": 300000,
    "yourScore": 0,
    "opponentScore": 0,
    "yourStatus": {
      "p1Status": {"x": 1000, "y": 2000, "vx": 0, "vy": 0},
      "p2Status": {"x": 1000, "y": 7000, "vx": 0, "vy": 0},
      "p3Status": {"x": 3000, "y": 3000, "vx": 0, "vy": 0},
      "p4Status": {"x": 3000, "y": 6000, "vx": 0, "vy": 0}
    },
    "opponentStatus": {
      "p1Status": {"x": 11000, "y": 2000, "vx": 0, "vy": 0},
      "p2Status": {"x": 11000, "y": 7000, "vx": 0, "vy": 0},
      "p3Status": {"x": 9000, "y": 3000, "vx": 0, "vy": 0},
      "p4Status": {"x": 9000, "y": 6000, "vx": 0, "vy": 0}
    },
    "ballStatus": {"x": 6000, "y": 4500, "vx": 0, "vy": 0}
  }'

echo ""
echo ""
echo "Test completed!"

# Clean up
kill $SERVER_PID
echo "Server stopped."