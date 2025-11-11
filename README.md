# Multi-Agent Workflow System

A multi-agent system demonstrating coordinated planning, execution, verification, and recovery using .NET and AI workflows.

## Overview

This system showcases how multiple AI agents work together through a workflow pattern:

1. **PlannerAgent**: Decomposes complex tasks into multi-step DAGs (Directed Acyclic Graphs)
2. **ExecutorAgent**: Implements planned steps by invoking tools and APIs
3. **VerifierAgent**: Evaluates execution quality using LLM-as-a-judge pattern
4. **RecoveryHandlerAgent**: Manages exceptions and implements recovery strategies

## Prerequisites

- .NET 9.0 SDK
- LM Studio (or compatible OpenAI API endpoint) running locally or remotely
- A language model that supports structured JSON outputs

## Configuration

### Provider selection

- `LLM_PROVIDER`: `LmStudio` (default) or `AzureOpenAI`. This controls which chat client is registered.

### LM Studio / local OpenAI-compatible endpoints

- `LMSTUDIO_ENDPOINT`: API endpoint (default: `http://localhost:1234/v1`)
- `LMSTUDIO_API_KEY`: API key for authentication (default: `lm-studio`)
- `LMSTUDIO_MODEL`: Model ID to use (default: `openai/gpt-oss-20b`)

### Azure OpenAI or proxy frontends

Set `LLM_PROVIDER=AzureOpenAI` and supply:

- `AZURE_OPENAI_ENDPOINT`: Base URL for your Azure OpenAI resource or proxy
- `AZURE_OPENAI_API_KEY`: API key (or proxy-issued key) to authenticate requests
- `AZURE_OPENAI_DEPLOYMENT`: Deployment name (maps to the chat model to invoke)
- `AZURE_OPENAI_API_VERSION`: Optional service version. Accepts either the enum name (`V2024_10_21`) or the raw version string (`2024-10-21-preview`). The latest supported version is used if omitted or unrecognized.

If you're routing through a proxy, point `AZURE_OPENAI_ENDPOINT` at the proxy URL—it must forward requests using the Azure OpenAI REST surface so the SDK can attach the required `api-version`.

### Shared agent tuning

- `MINIMUM_RATING`: Minimum quality rating for verification (default: 7)
- `MAX_ATTEMPTS`: Maximum retry attempts for recovery (default: 3)

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
   - Trigger workflow: `curl -X POST http://localhost:5000/run`
   - Check workflow status: `curl http://localhost:5000/runs/{workflowId}`

The POST response includes a `workflowId` and a `Location` header. Use those values with the `/runs/{workflowId}` endpoint to poll status (events, outputs, and errors) instead of tailing the console. Every log line and status update is tagged with the workflow ID for easier correlation.

## Project Structure

- `AgentLmLocal/` - Main application
  - `Agents/` - Agent implementations (Planner, Executor, Verifier, Recovery)
  - `Models/` - Data models and schemas
  - `Services/` - Shared services (LLM service, Agent factory)
  - `Workflow/` - Workflow event handling
  - `Configuration/` - Application configuration
- `docs/` - Documentation
  - `SERILOG-OTEL-HYBRID.md` - Logging architecture documentation

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

## How It Works

1. User submits a complex task
2. PlannerAgent analyzes and creates a DAG-based execution plan
3. ExecutorAgent executes each node in the plan
4. VerifierAgent checks the quality of execution results
5. RecoveryHandlerAgent handles failures with retry, rollback, skip, or escalate strategies
6. Results are returned to the user

## Based On

This implementation is based on agentic workflow patterns from AI research.
