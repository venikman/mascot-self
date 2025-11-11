using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Models;
using AgentLmLocal;
using AgentLmLocal.Services;
using AgentLmLocal.Workflow;

namespace WorkflowCustomAgentExecutorsSample.Agents;

public sealed class ExecutorAgent : InstrumentedAgent<WorkflowPlan>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly LlmService _llmService;
    private readonly Dictionary<string, object> _executionState = [];

    public int VerificationInterval { get; init; } = 3;

    private int _nodesExecuted;

    public ExecutorAgent(
        string id,
        AgentFactory agentFactory,
        LlmService llmService)
        : base(id)
    {
        _llmService = llmService;

        var instructions = """
            You are an expert executor agent that implements workflow steps.

            Your responsibilities:
            1. Execute workflow nodes in the specified order
            2. Handle tool calls and LLM invocations
            3. Manage state transitions between steps
            4. Validate input/output schemas
            5. Apply policy guards and safety checks
            6. Track execution progress and performance

            For each node you execute:
            - Validate that all dependencies are satisfied
            - Check input schema compliance
            - Execute the action (simulate tool calls or perform LLM operations)
            - Validate output schema compliance
            - Update execution state
            - Report execution results

            Simulate realistic execution times and potential errors for demonstration purposes.
            """;

        (_agent, _thread) = agentFactory.CreateAgent(instructions);
    }

    protected override async ValueTask ExecuteInstrumentedAsync(WorkflowPlan plan, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var results = new List<ExecutionResult>();

        foreach (var nodeId in plan.ExecutionOrder)
        {
            var node = plan.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null)
            {
                await context.AddEventAsync(
                    new ExecutionErrorEvent($"Node {nodeId} not found in plan"),
                    cancellationToken);
                continue;
            }

            var result = await ExecuteNodeAsync(node, plan, cancellationToken);
            results.Add(result);

            await context.AddEventAsync(new NodeExecutedEvent(result), cancellationToken);

            _executionState[nodeId] = result;
            _nodesExecuted++;

            if (_nodesExecuted >= VerificationInterval)
            {
                await context.SendMessageAsync(results, cancellationToken: cancellationToken);
                _nodesExecuted = 0;
                results.Clear();
            }

            if (result.Status == ExecutionStatus.Failure.ToLowerString())
            {
                await context.SendMessageAsync(result, cancellationToken: cancellationToken);
                break;
            }
        }

        if (results.Count > 0)
        {
            await context.SendMessageAsync(results, cancellationToken: cancellationToken);
        }

        await context.YieldOutputAsync(
            $"Workflow execution completed: {plan.Nodes.Count} nodes executed successfully.",
            cancellationToken);
    }

    private async Task<ExecutionResult> ExecuteNodeAsync(
        WorkflowNode node,
        WorkflowPlan plan,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();


        {
            foreach (var depId in node.Dependencies)
            {
                if (!_executionState.ContainsKey(depId))
                {
                    return new ExecutionResult
                    {
                        NodeId = node.Id,
                        Status = ExecutionStatus.Failure.ToLowerString(),
                        Error = $"Dependency {depId} not satisfied",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }

            string output;
            var nodeType = EnumExtensions.ParseNodeType(node.Type);
            switch (nodeType)
            {
                case NodeType.ToolCall:
                    output = await SimulateToolCallAsync(node, cancellationToken);
                    break;

                case NodeType.LlmInvocation:
                    output = await ExecuteLLMInvocationAsync(node, cancellationToken);
                    break;

                case NodeType.Conditional:
                    output = EvaluateConditional(node);
                    break;

                default:
                    output = $"Executed {node.Type}: {node.Description}";
                    break;
            }

            stopwatch.Stop();

            return new ExecutionResult
            {
                NodeId = node.Id,
                Status = ExecutionStatus.Success.ToLowerString(),
                Output = output,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    ["node_type"] = node.Type,
                    ["description"] = node.Description
                }
            };
        }
    }

    private async Task<string> SimulateToolCallAsync(WorkflowNode node, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        return $"Tool call completed: {node.Description}";
    }

    private async Task<string> ExecuteLLMInvocationAsync(WorkflowNode node, CancellationToken cancellationToken)
    {
        var prompt = $"Execute this step: {node.Description}";
        return await _llmService.InvokeAsync(_agent, _thread, prompt, cancellationToken);
    }

    private string EvaluateConditional(WorkflowNode node)
    {
        return $"Conditional evaluated: {node.Description}";
    }
}
