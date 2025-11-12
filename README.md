# Testing Guide

## Prerequisites
- .NET 10.0 SDK
- OpenAI-compatible endpoint (local or remote)

## Environment Setup
```bash
cp .env.example .env
# Set OPENAI_API_KEY, OPENAI_BASE_URL, OPENAI_MODEL
# Defaults: OPENAI_BASE_URL=http://127.0.0.1:1234/v1, OPENAI_MODEL=kat-dev-mlx
```

## Build & Run
```bash
cd AgentLmLocal
dotnet build
dotnet run
```

## API Tests
- Health: `curl http://localhost:5000/health`
- Chat: `curl -X POST http://localhost:5000/chat -H "Content-Type: application/json" -d '{"message":"Hello"}'`
- Start workflow: `curl -X POST http://localhost:5000/run -H "Content-Type: application/json" -d '{"task":"Create a mascot slogan"}'`
- Check status: `curl http://localhost:5000/runs/{workflowId}`

## Frontend Tests
```bash
cd AgentLmLocal/ClientApp
bun install
bun run dev   # http://localhost:5173
```
Interact with the chat UI; requests proxy to the backend `/chat`.

## Observability Checks
- Console outputs JSON logs (Serilog)
- Frontend telemetry posts to `/otel/traces`

**Backend Endpoints:**
- `POST /otel/traces` - Proxy endpoint that receives telemetry from frontend
- `POST /chat` - AI chat endpoint for the demo
- `Models/OtelModels.cs` - OTLP (OpenTelemetry Protocol) data models
- `Models/ApiModels.cs` - API request/response models

**Access the demo:**
1. Start the backend: `dotnet run` (from `AgentLmLocal/`)
2. For development: Navigate to `http://localhost:5173` (Bun dev server)
3. For production: Build first (`bun run build`), then navigate to `http://localhost:5000`

For detailed documentation, see [AgentLmLocal/ClientApp/README.md](AgentLmLocal/ClientApp/README.md) and [docs/FRONTEND-OTEL-EXAMPLE.md](docs/FRONTEND-OTEL-EXAMPLE.md).

## How It Works

1. User submits a complex task
2. PlannerAgent analyzes and creates a DAG-based execution plan
3. ExecutorAgent executes each node in the plan
4. VerifierAgent checks the quality of execution results
5. RecoveryHandlerAgent handles failures with retry, rollback, skip, or escalate strategies
6. Results are returned to the user

## Based On

This implementation is based on agentic workflow patterns from AI research.
