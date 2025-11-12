# Multi-Agent Workflow System

A multi-agent system demonstrating coordinated planning, execution, verification, and recovery using .NET and AI workflows.

## Overview

This system showcases how multiple AI agents work together through a workflow pattern:

1. **PlannerAgent**: Decomposes complex tasks into multi-step DAGs (Directed Acyclic Graphs)
2. **ExecutorAgent**: Implements planned steps by invoking tools and APIs
3. **VerifierAgent**: Evaluates execution quality using LLM-as-a-judge pattern
4. **RecoveryHandlerAgent**: Manages exceptions and implements recovery strategies

## Prerequisites

- .NET 10.0 SDK
- LM Studio (or compatible OpenAI API endpoint) running locally or remotely
- A language model that supports structured JSON outputs

## Configuration

This application supports **two LLM providers**. See **[CONFIGURATION.md](CONFIGURATION.md)** for complete setup guide.

### Quick Start

**For LM Studio (Local):**
```bash
cp .env.lmstudio.example .env
dotnet run
```

**For Azure OpenAI (Cloud):**
```bash
cp .env.azureopenai.example .env
# Edit .env with your Azure credentials
dotnet run
```

### Key Configuration

**Switch providers** by changing one variable:
- `LLM_PROVIDER=LmStudio` - Use local LM Studio or OpenAI-compatible endpoints
- `LLM_PROVIDER=AzureOpenAI` - Use Azure OpenAI Service

**LM Studio variables:**
- `LMSTUDIO_ENDPOINT` - API endpoint (default: `http://localhost:1234/v1`)
- `LMSTUDIO_API_KEY` - Authentication key (default: `lm-studio`)
- `LMSTUDIO_MODEL` - Model identifier (default: `openai/gpt-oss-20b`)

**Azure OpenAI variables:**
- `AZURE_OPENAI_ENDPOINT` - Azure resource URL (e.g., `https://your-name.openai.azure.com/`)
- `AZURE_OPENAI_API_KEY` - Azure API key
- `AZURE_OPENAI_DEPLOYMENT` - Deployment name (e.g., `gpt-4o`)
- `AZURE_OPENAI_API_VERSION` - API version (optional, defaults to latest)

**Workflow tuning:**
- `MINIMUM_RATING` - Quality threshold 1-10 (default: 7)
- `MAX_ATTEMPTS` - Maximum retry attempts (default: 3)

📚 **For detailed configuration, examples, and troubleshooting, see [CONFIGURATION.md](CONFIGURATION.md)**

## Running the Application

1. **Build the application**:
   ```bash
   cd AgentLmLocal
   dotnet build
   ```

2. **Run the application**:
   ```bash
   dotnet run
   ```

3. **Access the endpoints**:
   - Health check: `http://localhost:5000/health`
   - AI Chat UI: `http://localhost:5000` (Frontend OpenTelemetry example)
   - Trigger workflow: `curl -X POST http://localhost:5000/run -H "Content-Type: application/json" -d '{"task":"Plan a 3 course meal"}'`
   - Check workflow status: `curl http://localhost:5000/runs/{workflowId}`

The POST response includes a `workflowId` and a `Location` header. Use those values with the `/runs/{workflowId}` endpoint to poll status (events, outputs, and errors) instead of tailing the console. Every log line and status update is tagged with the workflow ID for easier correlation.

## Project Structure

- `AgentLmLocal/` - Main application
  - `Agents/` - Agent implementations (Planner, Executor, Verifier, Recovery)
  - `Models/` - Data models and schemas (including OTLP models)
  - `Services/` - Shared services (LLM service, Agent factory)
  - `Workflow/` - Workflow event handling
  - `Configuration/` - Application configuration
  - `wwwroot/` - Frontend application (AI chat with OpenTelemetry)
- `docs/` - Documentation
  - `SERILOG-OTEL-HYBRID.md` - Logging architecture documentation
  - `FRONTEND-OTEL-EXAMPLE.md` - Frontend OpenTelemetry integration guide

## Observability

This application uses a **hybrid observability approach**:

- **Serilog** for structured logging (JSON Lines to stdout)
- **OpenTelemetry** for distributed tracing
- **Automatic correlation** via TraceId/SpanId in logs

All logs are output in [Compact JSON format](https://github.com/serilog/serilog-formatting-compact) (one JSON object per line), making them ideal for log aggregation systems like Splunk, Elasticsearch, or CloudWatch.

**Example log output:**
```json
{"@t":"2025-11-11T14:30:45.123Z","@mt":"Workflow {WorkflowId} executing","@l":"Information","WorkflowId":"abc123","ServiceName":"AgentLmLocal","TraceId":"4bf92f3577b34da6","SpanId":"00f067aa0ba902b7"}
```

For complete details, see [docs/SERILOG-OTEL-HYBRID.md](docs/SERILOG-OTEL-HYBRID.md).

### Frontend OpenTelemetry Integration

The project includes a **React + TypeScript frontend** that demonstrates how to collect and export telemetry from browser-based applications.

**Tech Stack:**
- React 18 with TypeScript for type safety
- OpenTelemetry Web SDK with proper npm packages
- Custom React hooks for telemetry management
- Component-based architecture
- **Bun for everything**: package manager, bundler, dev server, and runtime
- Native TypeScript/TSX support with instant transpilation
- 3x faster than npm/webpack

**Development:**
```bash
cd AgentLmLocal/ClientApp
bun install
bun run dev  # Bun dev server on http://localhost:5173
```

**Production Build:**
```bash
cd AgentLmLocal/ClientApp
bun run build  # Bun's native bundler outputs to ../wwwroot
```

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
