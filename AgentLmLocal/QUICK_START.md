# Quick Start Guide - Agentic Workflow Agents

This guide will help you quickly get started with the agentic workflow agents as plugins.

## Quick Run

```bash
cd AgentLmLocal
dotnet run
```

## Available Agents (Plugins)

All agents are located in the `Agents/` directory and can be used as plugins in your workflows:

1. **PlannerAgent** - Breaks down complex tasks
2. **ExecutorAgent** - Executes the planned steps
3. **VerifierAgent** - Validates execution quality
4. **RecoveryHandlerAgent** - Handles errors and failures
5. **RetrieverAgent** - Retrieves external knowledge

## Using Agents as Plugins

### Basic Plugin Usage

```csharp
using WorkflowCustomAgentExecutorsSample.Agents;

// 1. Create your chat client
var chatClient = GetYourChatClient();

// 2. Instantiate agents (plugins)
var planner = new PlannerAgent("Planner", chatClient);
var executor = new ExecutorAgent("Executor", chatClient);
var verifier = new VerifierAgent("Verifier", chatClient);

// 3. Build workflow
var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, executor)
    .AddEdge(executor, verifier)
    .WithOutputFrom(verifier)
    .Build();

// 4. Execute
await using var run = await InProcessExecution.StreamAsync(workflow, "Your task");
await foreach (var evt in run.WatchStreamAsync())
{
    Console.WriteLine(evt);
}
```

### Configure Agent Behavior

Each agent has configurable properties:

```csharp
// Executor with custom verification interval
var executor = new ExecutorAgent("Executor", chatClient)
{
    VerificationInterval = 5 // Check every 5 nodes
};

// Verifier with custom quality threshold
var verifier = new VerifierAgent("Verifier", chatClient)
{
    MinimumQualityScore = 8,
    EnableSpeculativeExecution = false
};

// Recovery handler with custom retry settings
var recovery = new RecoveryHandlerAgent("Recovery", chatClient)
{
    MaxRetries = 5,
    EnableStateSnapshots = true
};

// Retriever with caching
var retriever = new RetrieverAgent("Retriever", chatClient)
{
    EnableCaching = true,
    MinimumRelevanceScore = 0.8
};
```

## Common Plugin Patterns

### Pattern 1: Simple Plan-Execute-Verify

```csharp
var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, executor)
    .AddEdge(executor, verifier)
    .WithOutputFrom(verifier)
    .Build();
```

### Pattern 2: With Recovery

```csharp
var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, executor)
    .AddEdge(executor, verifier)
    .AddEdge(verifier, recovery)      // On failure
    .AddEdge(recovery, executor)      // Retry
    .WithOutputFrom(verifier)
    .Build();
```

### Pattern 3: With Knowledge Retrieval

```csharp
var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, retriever)      // Get knowledge first
    .AddEdge(retriever, executor)     // Execute with knowledge
    .AddEdge(executor, verifier)
    .WithOutputFrom(verifier)
    .Build();
```

### Pattern 4: Full Coordination (Recommended)

```csharp
var workflow = new WorkflowBuilder(planner)
    .AddEdge(planner, executor)
    .AddEdge(executor, verifier)
    .AddEdge(verifier, recovery)
    .AddEdge(recovery, executor)
    .AddEdge(executor, retriever)     // Optional knowledge
    .AddEdge(retriever, executor)
    .WithOutputFrom(verifier)
    .Build();
```

## Data Models

Import models for type safety:

```csharp
using WorkflowCustomAgentExecutorsSample.Models;

// Use in your code
var query = new RetrievalQuery
{
    Query = "Find information about quantum computing",
    MaxResults = 10
};
```

Available models:
- `WorkflowPlan` - Complete task plan
- `WorkflowNode` - Single step in plan
- `ExecutionResult` - Execution outcome
- `VerificationResult` - Quality check result
- `RecoveryStrategy` - Error recovery plan
- `RetrievalQuery` - Knowledge query
- `RetrievedKnowledge` - Retrieved results

## Environment Setup

Set your LLM provider:

```bash
# LM Studio (local)
export LMSTUDIO_ENDPOINT="http://localhost:1234/v1"
export LMSTUDIO_API_KEY="lm-studio"
export LMSTUDIO_MODEL="your-model-name"

# Or use Azure OpenAI, OpenAI, etc.
```

## Creating Custom Agents

Create your own plugin agent:

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

public sealed class MyCustomAgent : Executor<string>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    public MyCustomAgent(string id, IChatClient chatClient) : base(id)
    {
        var options = new ChatClientAgentOptions(
            instructions: "Your custom agent instructions");
        _agent = new ChatClientAgent(chatClient, options);
        _thread = _agent.GetNewThread();
    }

    public override async ValueTask HandleAsync(
        string input,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await _agent.RunAsync(input, _thread, cancellationToken);
        await context.SendMessageAsync(result.Text, cancellationToken);
    }
}
```

## Monitoring Events

Listen to agent events:

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    switch (evt)
    {
        case PlanGeneratedEvent planEvt:
            Console.WriteLine($"Plan created: {planEvt}");
            break;
        case NodeExecutedEvent execEvt:
            Console.WriteLine($"Node executed: {execEvt}");
            break;
        case VerificationCompletedEvent verifyEvt:
            Console.WriteLine($"Verified: {verifyEvt}");
            break;
        case RecoveryStrategyDeterminedEvent recoveryEvt:
            Console.WriteLine($"Recovery: {recoveryEvt}");
            break;
        case RetrievalEvent retrievalEvt:
            Console.WriteLine($"Retrieved: {retrievalEvt}");
            break;
        case WorkflowOutputEvent output:
            Console.WriteLine($"OUTPUT: {output}");
            break;
    }
}
```

## Examples

See these files for complete examples:
- `AgenticWorkflowExample.cs` - Full multi-agent coordination

## Visualization

Workflow visualizations are automatically generated in:
```
bin/Debug/net9.0/WorkflowVisualization/
```

Files generated:
- `*.dot` - GraphViz format
- `*.mmd` - Mermaid diagram
- `*.svg` - SVG image (if GraphViz installed)
- `*.png` - PNG image (if GraphViz installed)

## Need Help?

1. Check `README_AGENTS.md` for detailed documentation
2. Review example code in `AgenticWorkflowExample.cs`
3. Examine agent implementations in `Agents/` directory
4. See the research paper: https://www.emergentmind.com/research/5ec25fafbefce83f72059ba5

## Tips

1. **Start Simple**: Begin with Plan → Execute → Verify
2. **Add Recovery**: Add error handling for production
3. **Use Knowledge**: Add Retriever for RAG capabilities
4. **Monitor Events**: Watch events to understand flow
5. **Tune Parameters**: Adjust agent properties for your use case
6. **Custom Agents**: Create specialized agents for your domain

## Common Issues

**Issue**: Agents not responding
- Check LLM endpoint is running
- Verify API key and model name
- Check network connectivity

**Issue**: Low quality scores
- Adjust `MinimumQualityScore` in VerifierAgent
- Enable `EnableSpeculativeExecution` for flexibility
- Review agent instructions

**Issue**: Too many retries
- Reduce `MaxRetries` in RecoveryHandlerAgent
- Check root cause of failures
- Improve error handling in ExecutorAgent

## Next Steps

1. Run the examples
2. Experiment with different agent combinations
3. Create custom agents for your domain
4. Integrate with your existing workflows
5. Share your agent plugins with the community!
