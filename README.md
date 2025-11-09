# Agentic Workflow System

This repository implements an **agentic workflow system** based on the research paper: [Agentic Workflows](https://www.emergentmind.com/research/5ec25fafbefce83f72059ba5)

## What's Inside

A complete multi-agent system built on Microsoft Agents AI framework with 5 specialized agent plugins that work together to accomplish complex tasks.

## Quick Start

```bash
cd AgentLmLocal
dotnet run              # Run agentic workflow example
```

## Agent Plugins

All agents are implemented as **reusable plugins** that can be added to any workflow:

### 1. **PlannerAgent** (`Agents/PlannerAgent.cs`)
Decomposes complex tasks into structured DAG (Directed Acyclic Graph) plans.
- Analyzes tasks and breaks them into steps
- Identifies dependencies between steps
- Determines execution order
- Estimates complexity

### 2. **ExecutorAgent** (`Agents/ExecutorAgent.cs`)
Executes planned steps by invoking tools and APIs.
- Executes workflow nodes in order
- Handles tool calls and LLM invocations
- Manages state transitions
- Tracks performance

### 3. **VerifierAgent** (`Agents/VerifierAgent.cs`)
Acts as "LLM-as-a-judge" to evaluate execution quality.
- Evaluates quality and correctness (1-10 scale)
- Provides constructive feedback
- Supports speculative execution
- Triggers rollback on critical failures

### 4. **RecoveryHandlerAgent** (`Agents/RecoveryHandlerAgent.cs`)
Manages exceptions with intelligent recovery strategies.
- Analyzes root causes
- Implements recovery patterns:
  - **Retry**: Transient errors
  - **Rollback**: State corruption
  - **Skip**: Non-critical steps
  - **Escalate**: Critical errors
- Manages state restoration

### 5. **RetrieverAgent** (`Agents/RetrieverAgent.cs`)
Retrieval-augmented generation (RAG) for external knowledge.
- Queries knowledge bases
- Ranks by relevance
- Caches results
- Provides source attribution

## Architecture

```
┌─────────────┐
│   Planner   │ → Decomposes task into DAG
└──────┬──────┘
       ↓ WorkflowPlan
┌─────────────┐
│  Executor   │ → Executes nodes
└──────┬──────┘
       ↓ ExecutionResult
┌─────────────┐      ┌─────────────┐
│  Verifier   │─────→│  Recovery   │ (On failure)
└──────┬──────┘      └──────┬──────┘
       │ Success             │ Retry/Rollback
       ↓                     ↓
    Output              Retry Loop

    ┌─────────────┐
    │  Retriever  │ → External knowledge (as needed)
    └─────────────┘
```

## Project Structure

```
AgentLmLocal/
├── Agents/                          # Agent plugins
│   ├── PlannerAgent.cs             # Task decomposition
│   ├── ExecutorAgent.cs            # Step execution
│   ├── VerifierAgent.cs            # Quality verification
│   ├── RecoveryHandlerAgent.cs     # Error recovery
│   └── RetrieverAgent.cs           # Knowledge retrieval
├── Models/                          # Data models
│   └── WorkflowModels.cs           # Workflow data structures
├── Visualization/                   # Workflow visualization
│   └── WorkflowVisualizationRecorder.cs
├── AgenticWorkflowExample.cs       # Multi-agent example
├── Program.cs                       # Entry point
├── README_AGENTS.md                # Detailed documentation
└── QUICK_START.md                  # Quick reference guide
```

## Using Agents as Plugins

### Basic Example

```csharp
using WorkflowCustomAgentExecutorsSample.Agents;

// 1. Create agents
var planner = new PlannerAgent("Planner", chatClient);
var executor = new ExecutorAgent("Executor", chatClient);
var verifier = new VerifierAgent("Verifier", chatClient);

// 2. Build workflow
var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, executor)    // Plan → Execute
    .AddEdge(executor, verifier)   // Execute → Verify
    .WithOutputFrom(verifier)
    .Build();

// 3. Run workflow
await using var run = await InProcessExecution.StreamAsync(
    workflow,
    input: "Your complex task");

await foreach (var evt in run.WatchStreamAsync())
{
    Console.WriteLine(evt);
}
```

### With Error Recovery

```csharp
var recovery = new RecoveryHandlerAgent("Recovery", chatClient);

var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, executor)
    .AddEdge(executor, verifier)
    .AddEdge(verifier, recovery)    // Handle failures
    .AddEdge(recovery, executor)    // Retry
    .WithOutputFrom(verifier)
    .Build();
```

## Key Features

- ✅ **DAG-based Planning**: Multi-step task decomposition
- ✅ **LLM-as-a-Judge**: Quality verification (1-10 scoring)
- ✅ **Speculative Execution**: Continue on minor issues with rollback
- ✅ **Smart Recovery**: Retry, rollback, skip, or escalate
- ✅ **RAG Integration**: External knowledge retrieval
- ✅ **State Management**: Snapshots for rollback
- ✅ **Multi-Agent Coordination**: Concurrent execution with dependencies
- ✅ **Event Streaming**: Real-time workflow monitoring
- ✅ **Workflow Visualization**: Mermaid, DOT, SVG, PNG output

## Configuration

Set environment variables for your LLM provider:

```bash
# LM Studio (local)
export LMSTUDIO_ENDPOINT="http://localhost:1234/v1"
export LMSTUDIO_API_KEY="lm-studio"
export LMSTUDIO_MODEL="openai/gpt-oss-20b"
```

Compatible with:
- LM Studio (local LLMs)
- Azure OpenAI
- OpenAI API
- Any OpenAI-compatible endpoint

## Documentation

- **[README_AGENTS.md](AgentLmLocal/README_AGENTS.md)** - Comprehensive agent documentation
- **[QUICK_START.md](AgentLmLocal/QUICK_START.md)** - Quick reference guide
- **[AgenticWorkflowExample.cs](AgentLmLocal/AgenticWorkflowExample.cs)** - Working example

## Examples

### Example 1: Complex Multi-Step Task
```
Task: Create a comprehensive marketing campaign for a new AI-powered
productivity app, including market research, target audience analysis,
content strategy, and launch timeline.

Workflow:
1. Planner decomposes into steps
2. Executor runs each step
3. Verifier checks quality
4. Recovery handles any failures
```

### Example 2: Knowledge-Augmented Task
```
Task: Research and summarize the latest trends in quantum computing
applications for 2024, then create a technical presentation outline.

Workflow:
1. Planner creates research plan
2. Retriever fetches knowledge
3. Executor processes information
4. Verifier validates outputs
```

## Research Foundation

Based on the agentic workflows pattern described in the research paper. Key concepts implemented:

- ✅ Multi-step DAG decomposition
- ✅ Typed node schemas (tool_call, llm_invocation, conditional)
- ✅ LLM-as-a-judge verification
- ✅ Speculative execution with rollback
- ✅ Recovery taxonomy (retry, rollback, skip, escalate)
- ✅ Multi-agent coordination
- ✅ Episodic and semantic memory
- ✅ External knowledge binding (RAG)
- ✅ Policy enforcement
- ✅ Provenance tracking

## Extending the System

### Create Custom Agent

```csharp
public sealed class MyAgent : Executor<MyInput>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public MyAgent(string id, IChatClient chatClient) : base(id)
    {
        var options = new ChatClientAgentOptions(
            instructions: "Your custom instructions");
        _agent = new ChatClientAgent(chatClient, options);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask HandleAsync(
        MyInput input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        // Your logic here
        var result = await _agent.RunAsync(input, _thread, cancellationToken);
        await context.SendMessageAsync(result, cancellationToken);
    }
}
```

## Build & Run

```bash
# Build
cd AgentLmLocal
dotnet build

# Run agentic workflow
dotnet run
```

## Workflow Visualizations

Generated automatically in `bin/Debug/net9.0/WorkflowVisualization/`:
- `*.mmd` - Mermaid diagrams
- `*.dot` - GraphViz DOT files
- `*.svg` - SVG images (requires GraphViz)
- `*.png` - PNG images (requires GraphViz)

## Requirements

- .NET 9.0
- LM Studio or compatible LLM endpoint
- Optional: GraphViz (for PNG/SVG visualization)

## License

Uses Microsoft Agents AI framework.

## Contributing

1. Add new agents in `Agents/` directory
2. Extend models in `Models/WorkflowModels.cs`
3. Create examples in `AgenticWorkflowExample.cs`
4. Update documentation

## Support

- Check examples in `AgenticWorkflowExample.cs`
- Review agent code in `Agents/` directory
- Read detailed docs in `README_AGENTS.md`
- Consult research paper: https://www.emergentmind.com/research/5ec25fafbefce83f72059ba5
