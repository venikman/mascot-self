# Agentic Workflow Plugin Agents

This repository contains a complete implementation of the agentic workflow pattern based on the research paper: [Agentic Workflows Research](https://www.emergentmind.com/research/5ec25fafbefce83f72059ba5)

## Overview

These agents implement a sophisticated multi-agent system where specialized agents work together in a directed acyclic graph (DAG) structure to accomplish complex tasks.

## Agents

### 1. PlannerAgent
**Location:** `Agents/PlannerAgent.cs`

**Purpose:** Decomposes complex tasks into multi-step DAGs (Directed Acyclic Graphs).

**Capabilities:**
- Analyzes incoming tasks and generates structured plans
- Creates typed nodes (tool_call, llm_invocation, conditional)
- Manages dependencies between steps
- Produces optimal execution order
- Estimates task complexity

**Usage:**
```csharp
var planner = new PlannerAgent("Planner", chatClient);
```

### 2. ExecutorAgent
**Location:** `Agents/ExecutorAgent.cs`

**Purpose:** Implements planned steps by invoking tools and APIs.

**Capabilities:**
- Executes workflow nodes according to execution order
- Handles typed input/output schemas
- Enforces policy guards
- Manages state transitions
- Tracks execution progress and performance

**Configuration:**
```csharp
var executor = new ExecutorAgent("Executor", chatClient)
{
    VerificationInterval = 3 // Verify every 3 nodes
};
```

### 3. VerifierAgent
**Location:** `Agents/VerifierAgent.cs`

**Purpose:** Acts as "LLM-as-a-judge" to evaluate workflow step outputs.

**Capabilities:**
- Evaluates quality and correctness of execution results
- Assigns quality scores (1-10)
- Provides constructive feedback
- Determines if rollback is needed
- Supports speculative execution

**Configuration:**
```csharp
var verifier = new VerifierAgent("Verifier", chatClient)
{
    MinimumQualityScore = 7,
    EnableSpeculativeExecution = true
};
```

### 4. RecoveryHandlerAgent
**Location:** `Agents/RecoveryHandlerAgent.cs`

**Purpose:** Manages exceptions and implements recovery strategies.

**Capabilities:**
- Analyzes root causes of failures
- Classifies error types (transient, permanent, configuration, etc.)
- Implements recovery patterns:
  - **Retry**: For transient errors
  - **Rollback**: For state corruption
  - **Skip**: For non-critical steps
  - **Escalate**: For critical errors
- Manages state restoration

**Configuration:**
```csharp
var recoveryHandler = new RecoveryHandlerAgent("RecoveryHandler", chatClient)
{
    MaxRetries = 3,
    EnableStateSnapshots = true
};
```

### 5. RetrieverAgent
**Location:** `Agents/RetrieverAgent.cs`

**Purpose:** Binds to external knowledge systems for retrieval-augmented generation (RAG).

**Capabilities:**
- Queries external knowledge bases
- Ranks results by relevance
- Filters by quality threshold
- Caches retrieval results
- Provides source attribution

**Configuration:**
```csharp
var retriever = new RetrieverAgent("Retriever", chatClient)
{
    EnableCaching = true,
    MinimumRelevanceScore = 0.7
};
```

## Data Models

**Location:** `Models/WorkflowModels.cs`

Key models include:
- `WorkflowPlan`: Complete workflow with nodes and execution order
- `WorkflowNode`: Individual step in the workflow DAG
- `ExecutionResult`: Result of executing a workflow step
- `VerificationResult`: Verification feedback on execution quality
- `RecoveryStrategy`: Recovery plan for handling failures
- `RetrievalQuery`: Query for external knowledge
- `RetrievedKnowledge`: Retrieved knowledge with relevance scores

## Usage Example

See `AgenticWorkflowExample.cs` for a complete demonstration of multi-agent coordination.

### Basic Workflow Setup

```csharp
// Create agents
var planner = new PlannerAgent("Planner", chatClient);
var executor = new ExecutorAgent("Executor", chatClient);
var verifier = new VerifierAgent("Verifier", chatClient);
var recoveryHandler = new RecoveryHandlerAgent("RecoveryHandler", chatClient);

// Build workflow with agent coordination
var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, executor)      // Plan → Execute
    .AddEdge(executor, verifier)     // Execute → Verify
    .AddEdge(verifier, recoveryHandler) // Verify → Recover (on failure)
    .AddEdge(recoveryHandler, executor) // Recover → Retry
    .WithOutputFrom(verifier)
    .Build();

// Execute workflow
await using var run = await InProcessExecution.StreamAsync(
    workflow,
    input: "Your complex task here");

await foreach (var evt in run.WatchStreamAsync())
{
    Console.WriteLine(evt);
}
```

## Key Features

### Multi-Agent Coordination
- Agents operate concurrently within dependency constraints
- Share episodic memory (per-run context)
- Share semantic memory (reusable skills)
- Policy enforcement at tool boundaries
- Provenance tracking across all decisions

### Error Handling
- Comprehensive error classification
- Automatic retry with exponential backoff
- State snapshots for rollback
- Graceful degradation with speculative execution
- Escalation paths for critical failures

### Quality Assurance
- Selective verification of execution results
- Quality scoring (1-10 scale)
- Speculative execution for minor issues
- Rollback on critical failures
- Continuous improvement through feedback

### Knowledge Integration
- Retrieval-augmented generation (RAG)
- External knowledge binding
- Result caching for efficiency
- Relevance scoring and filtering
- Source attribution

## Running the Example

```bash
dotnet run
```
This runs the full multi-agent coordination example.

## Configuration

Set these environment variables to configure the LLM backend:

```bash
export LMSTUDIO_ENDPOINT="http://localhost:1234/v1"
export LMSTUDIO_API_KEY="lm-studio"
export LMSTUDIO_MODEL="openai/gpt-oss-20b"
```

Or use Azure OpenAI, OpenAI, or any compatible endpoint.

## Extending the Agents

### Creating Custom Agents

Inherit from `Executor<TInput>` or `Executor`:

```csharp
public sealed class MyCustomAgent : Executor<MyInput>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public MyCustomAgent(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new(
            instructions: "Your agent instructions here");

        _agent = new ChatClientAgent(chatClient, agentOptions);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask HandleAsync(
        MyInput message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Your agent logic here
        var result = await _agent.RunAsync(message, _thread, cancellationToken);
        await context.SendMessageAsync(result, cancellationToken);
    }
}
```

### Adding Custom Events

```csharp
public sealed class MyCustomEvent(MyData data) : WorkflowEvent(data)
{
    public override string ToString() => $"My Event: {data}";
}

// Emit in your agent:
await context.AddEventAsync(new MyCustomEvent(data), cancellationToken);
```

## Architecture

```
┌─────────────┐
│   Planner   │ (Decomposes tasks into DAG)
└──────┬──────┘
       │ WorkflowPlan
       ▼
┌─────────────┐
│  Executor   │ (Executes nodes)
└──────┬──────┘
       │ ExecutionResult
       ▼
┌─────────────┐      ┌─────────────┐
│  Verifier   │─────▶│  Recovery   │ (On failure)
└──────┬──────┘      └──────┬──────┘
       │ Success             │ Retry/Rollback
       ▼                     ▼
    Output            ┌─────────────┐
                      │  Executor   │ (Retry)
                      └─────────────┘

                      ┌─────────────┐
                      │  Retriever  │ (Knowledge)
                      └─────────────┘
                            ▲ │
                            └─┘ (Augments any agent)
```

## Research Reference

This implementation is based on the agentic workflows pattern described in:
- Paper: https://www.emergentmind.com/research/5ec25fafbefce83f72059ba5

Key concepts implemented:
- ✅ DAG-based workflow decomposition
- ✅ Typed node schemas (tool_call, llm_invocation, conditional)
- ✅ LLM-as-a-judge verification
- ✅ Speculative execution with rollback
- ✅ Recovery pattern taxonomy (retry, rollback, skip, escalate)
- ✅ Multi-agent coordination
- ✅ Episodic and semantic memory
- ✅ External knowledge binding (RAG)
- ✅ Policy enforcement
- ✅ Provenance tracking

## License

This implementation follows the Microsoft Agents AI framework licensing.

## Contributing

To add new agents:
1. Create a new file in `Agents/` directory
2. Inherit from `Executor<T>` or `Executor`
3. Implement the `HandleAsync` method
4. Add custom events if needed
5. Update this README with documentation

## Support

For issues or questions:
- Check the example code in `AgenticWorkflowExample.cs`
- Review agent implementations in `Agents/` directory
- Consult the original research paper
