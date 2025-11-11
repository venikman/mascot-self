using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Models;
using AgentLmLocal;
using AgentLmLocal.Services;
using AgentLmLocal.Workflow;

namespace WorkflowCustomAgentExecutorsSample.Agents;

/// <summary>
/// ExecutorAgent: Implements planned workflow steps by invoking tools and APIs.
///
/// This agent takes workflow plans and executes each node according to the execution order.
/// It handles typed input/output schemas, enforces policy guards, and manages state
/// transitions during workflow progression.
///
/// Based on the agentic workflows pattern described in the research paper.
/// </summary>
public sealed class ExecutorAgent : InstrumentedAgent<WorkflowPlan>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly LlmService _llmService;
    private readonly Dictionary<string, object> _executionState = [];

    /// <summary>
    /// Maximum number of nodes to execute before requiring verification.
    /// </summary>
    public int VerificationInterval { get; init; } = 3;

    private int _nodesExecuted;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutorAgent"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the executor agent.</param>
    /// <param name="agentFactory">Factory for creating AI agents.</param>
    /// <param name="llmService">Service for LLM invocations.</param>
    /// <param name="logger">Logger instance.</param>
    public ExecutorAgent(
        string id,
        AgentFactory agentFactory,
        LlmService llmService,
        ILogger<ExecutorAgent> logger)
        : base(id, logger)
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

    /// <summary>
    /// Handles workflow plan execution.
    /// </summary>
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

            // Execute the node
            var result = await ExecuteNodeAsync(node, plan, cancellationToken);
            results.Add(result);

            // Emit execution event
            await context.AddEventAsync(new NodeExecutedEvent(result), cancellationToken);

            // Store result in state for dependent nodes
            _executionState[nodeId] = result;
            _nodesExecuted++;

            // Check if verification is needed
            if (_nodesExecuted >= VerificationInterval)
            {
                // Send results for verification
                await context.SendMessageAsync(results, cancellationToken: cancellationToken);
                _nodesExecuted = 0;
                results.Clear();
            }

            // Stop if execution failed
            if (result.Status == ExecutionStatus.Failure.ToLowerString())
            {
                await context.SendMessageAsync(result, cancellationToken: cancellationToken);
                break;
            }
        }

        // Send any remaining results
        if (results.Count > 0)
        {
            await context.SendMessageAsync(results, cancellationToken: cancellationToken);
        }

        // Emit completion event
        await context.YieldOutputAsync(
            $"Workflow execution completed: {plan.Nodes.Count} nodes executed successfully.",
            cancellationToken);
    }

    /// <summary>
    /// Executes a single workflow node.
    /// </summary>
    private async Task<ExecutionResult> ExecuteNodeAsync(
        WorkflowNode node,
        WorkflowPlan plan,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();


        {
            // Check dependencies
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

            // Simulate execution based on node type
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
        // Simulate tool call with delay
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
        // Simple conditional evaluation
        return $"Conditional evaluated: {node.Description}";
    }
}
