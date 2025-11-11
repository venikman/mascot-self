#!/bin/bash
# Test script to verify Serilog + OTEL hybrid logging

echo "🧪 Testing Hybrid Logging Setup..."
echo "================================"
echo ""

cd AgentLmLocal

echo "📦 Building application..."
dotnet build --no-restore > /dev/null 2>&1

echo "🚀 Starting application (will run for 5 seconds)..."
echo ""

# Start the application in background
timeout 5s dotnet run --no-build 2>&1 | head -n 20 &
APP_PID=$!

# Wait a bit for startup
sleep 2

echo "📨 Sending test request to /run endpoint..."
curl -X POST http://localhost:5000/run \
  -H "Content-Type: application/json" \
  -d '{"task":"Test logging with tracing"}' \
  -s > /dev/null 2>&1

echo ""
echo "⏳ Waiting for logs (3 seconds)..."
sleep 3

echo ""
echo "✅ Test complete! Check the logs above for:"
echo "   - Single-line JSON format (Compact JSON)"
echo "   - TraceId and SpanId fields"
echo "   - Structured attributes"
echo "   - ServiceName, MachineName, EnvironmentName enrichments"
echo ""
