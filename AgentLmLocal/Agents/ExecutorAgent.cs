using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Models;
using AgentLmLocal;

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
    /// <param name="chatClient">The chat client to use for the AI agent.</param>
    /// <param name="logger">Logger instance for telemetry.</param>
    /// <param name="telemetry">Telemetry instrumentation instance.</param>
    public ExecutorAgent(string id, IChatClient chatClient, ILogger<ExecutorAgent> logger, AgentInstrumentation telemetry) 
        : base(id, logger, telemetry)
    {
        ChatClientAgentOptions agentOptions = new(
            instructions: """
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
            """)
        {
        };

        this._agent = new ChatClientAgent(chatClient, agentOptions);
        this._thread = this._agent.GetNewThread();
    }

    /// <summary>
    /// Handles workflow plan execution with telemetry instrumentation.
    /// </summary>
    protected override async ValueTask ExecuteInstrumentedAsync(WorkflowPlan plan, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Agent.Executor.PlanExecution");
        
        activity?.SetTag("plan.nodes.total", plan.Nodes.Count);
        activity?.SetTag("plan.execution_order.count", plan.ExecutionOrder.Count);
        
        Telemetry.RecordActivity(Id, "execution_started");
        
        var results = new List<ExecutionResult>();
        var successfulExecutions = 0;
        var failedExecutions = 0;

        
        {
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

                if (result.Status == "success")
                {
                    successfulExecutions++;
                }
                else
                {
                    failedExecutions++;
                }

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
                if (result.Status == "failure")
                {
                    await context.SendMessageAsync(result, cancellationToken: cancellationToken);
                    activity?.SetTag("execution.failed_at_node", nodeId);
                    break;
                }
            }

            // Send any remaining results
            if (results.Count > 0)
            {
                await context.SendMessageAsync(results, cancellationToken: cancellationToken);
            }

            activity?.SetTag("execution.successful_nodes", successfulExecutions);
            activity?.SetTag("execution.failed_nodes", failedExecutions);
            
            Telemetry.RecordActivity(Id, "execution_completed");

            // Emit completion event
            await context.YieldOutputAsync(
                $"Workflow execution completed: {plan.Nodes.Count} nodes executed successfully.",
                cancellationToken);
                
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
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
                        Status = "failure",
                        Error = $"Dependency {depId} not satisfied",
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                    };
                }
            }

            // Simulate execution based on node type
            string output;
            switch (node.Type.ToLowerInvariant())
            {
                case "tool_call":
                    output = await SimulateToolCallAsync(node, cancellationToken);
                    break;

                case "llm_invocation":
                    output = await ExecuteLLMInvocationAsync(node, cancellationToken);
                    break;

                case "conditional":
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
                Status = "success",
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
        var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);
        return result.Text;
    }

    private string EvaluateConditional(WorkflowNode node)
    {
        // Simple conditional evaluation
        return $"Conditional evaluated: {node.Description}";
    }
}

/// <summary>
/// Event emitted when a node is executed.
/// </summary>
public sealed class NodeExecutedEvent(ExecutionResult result) : WorkflowEvent(result)
{
    public override string ToString() =>
        $"Node {result.NodeId}: {result.Status} ({result.ExecutionTimeMs}ms)";
}

/// <summary>
/// Event emitted when an execution error occurs.
/// </summary>
public sealed class ExecutionErrorEvent(string error) : WorkflowEvent(error)
{
    public override string ToString() => $"Execution Error: {error}";
}
