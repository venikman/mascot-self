using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Models;

namespace AgentLmLocal.Workflow;

public sealed class PlanGeneratedEvent(WorkflowPlan plan) : WorkflowEvent(plan)
{
    public override string ToString()
    {
        var nodeCount = plan.Nodes.Count;
        var complexity = plan.EstimatedComplexity;
        return $"Plan Generated: {nodeCount} nodes, complexity: {complexity}\nTask: {plan.Task}";
    }
}

public sealed class NodeExecutedEvent(ExecutionResult result) : WorkflowEvent(result)
{
    public override string ToString() =>
        $"Node {result.NodeId}: {result.Status} ({result.ExecutionTimeMs}ms)";
}

public sealed class ExecutionErrorEvent(string error) : WorkflowEvent(error)
{
    public override string ToString() => $"Execution Error: {error}";
}

public sealed class VerificationCompletedEvent(VerificationResult result) : WorkflowEvent(result)
{
    public override string ToString()
    {
        var status = result.Passed ? "PASSED" : "FAILED";
        return $"Verification {status} for {result.NodeId}: Score {result.QualityScore}/10\n" +
               $"Feedback: {result.Feedback}";
    }
}

public sealed class RecoveryStrategyDeterminedEvent(RecoveryStrategy strategy) : WorkflowEvent(strategy)
{
    public override string ToString() =>
        $"Recovery Strategy: {strategy.RecoveryAction}\n" +
        $"Root Cause: {strategy.RootCause}\n" +
        $"State Restoration: {strategy.StateRestorationNeeded}";
}

public sealed class RecoveryActionEvent(string action) : WorkflowEvent(action)
{
    public override string ToString() => $"Recovery Action: {action}";
}
