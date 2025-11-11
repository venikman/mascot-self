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

Configure the following environment variables (or use defaults):

- `LMSTUDIO_ENDPOINT`: The API endpoint (default: `http://localhost:1234/v1`)
- `LMSTUDIO_API_KEY`: API key for authentication (default: `lm-studio`)
- `LMSTUDIO_MODEL`: The model ID to use (default: `openai/gpt-oss-20b`)
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

## Project Structure

- `AgentLmLocal/` - Main application
  - `Agents/` - Agent implementations (Planner, Executor, Verifier, Recovery)
  - `Models/` - Data models and schemas
  - `Services/` - Shared services (LLM service, Agent factory)
  - `Workflow/` - Workflow event handling
  - `Configuration/` - Application configuration

## How It Works

1. User submits a complex task
2. PlannerAgent analyzes and creates a DAG-based execution plan
3. ExecutorAgent executes each node in the plan
4. VerifierAgent checks the quality of execution results
5. RecoveryHandlerAgent handles failures with retry, rollback, skip, or escalate strategies
6. Results are returned to the user

## Based On

This implementation is based on agentic workflow patterns from AI research.