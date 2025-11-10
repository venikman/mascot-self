using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Models;

namespace AgentLmLocal.Workflow;

/// <summary>
/// Event emitted when a workflow plan is generated.
/// </summary>
public sealed class PlanGeneratedEvent(WorkflowPlan plan) : WorkflowEvent(plan)
{
    public override string ToString()
    {
        var nodeCount = plan.Nodes.Count;
        var complexity = plan.EstimatedComplexity;
        return $"Plan Generated: {nodeCount} nodes, complexity: {complexity}\nTask: {plan.Task}";
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

/// <summary>
/// Event emitted when verification is completed.
/// </summary>
public sealed class VerificationCompletedEvent(VerificationResult result) : WorkflowEvent(result)
{
    public override string ToString()
    {
        var status = result.Passed ? "PASSED" : "FAILED";
        return $"Verification {status} for {result.NodeId}: Score {result.QualityScore}/10\n" +
               $"Feedback: {result.Feedback}";
    }
}

/// <summary>
/// Event emitted when a recovery strategy is determined.
/// </summary>
public sealed class RecoveryStrategyDeterminedEvent(RecoveryStrategy strategy) : WorkflowEvent(strategy)
{
    public override string ToString() =>
        $"Recovery Strategy: {strategy.RecoveryAction}\n" +
        $"Root Cause: {strategy.RootCause}\n" +
        $"State Restoration: {strategy.StateRestorationNeeded}";
}

/// <summary>
/// Event emitted during recovery actions.
/// </summary>
public sealed class RecoveryActionEvent(string action) : WorkflowEvent(action)
{
    public override string ToString() => $"Recovery Action: {action}";
}
