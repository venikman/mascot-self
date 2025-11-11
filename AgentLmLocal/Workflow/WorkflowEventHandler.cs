using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Models;

namespace AgentLmLocal.Workflow;

public sealed class WorkflowEventHandler
{
    private readonly Dictionary<string, int> _eventCounts;

    public WorkflowEventHandler()
    {
        _eventCounts = new Dictionary<string, int>
        {
            [nameof(PlanGeneratedEvent)] = 0,
            [nameof(NodeExecutedEvent)] = 0,
            [nameof(VerificationCompletedEvent)] = 0,
            [nameof(RecoveryStrategyDeterminedEvent)] = 0,
            [nameof(RecoveryActionEvent)] = 0,
            [nameof(WorkflowOutputEvent)] = 0
        };
    }

    public async Task HandleEventAsync(WorkflowEvent evt)
    {
        var eventType = evt.GetType().Name;
        _eventCounts[eventType] = _eventCounts.GetValueOrDefault(eventType, 0) + 1;

        switch (evt)
        {
            case PlanGeneratedEvent planEvent:
                HandlePlanGenerated(planEvent);
                break;

            case NodeExecutedEvent nodeEvent:
                HandleNodeExecuted(nodeEvent);
                break;

            case VerificationCompletedEvent verificationEvent:
                HandleVerificationCompleted(verificationEvent);
                break;

            case RecoveryStrategyDeterminedEvent recoveryEvent:
                HandleRecoveryStrategyDetermined(recoveryEvent);
                break;

            case RecoveryActionEvent actionEvent:
                HandleRecoveryAction(actionEvent);
                break;

            case WorkflowOutputEvent outputEvent:
                HandleWorkflowOutput(outputEvent);
                break;

            default:
                Console.WriteLine($"[{eventType}] {evt}");
                break;
        }

        await Task.CompletedTask;
    }

    public Dictionary<string, int> GetEventCounts() => new(_eventCounts);

    private void HandlePlanGenerated(PlanGeneratedEvent evt)
    {
        Console.WriteLine($"[{nameof(PlanGeneratedEvent)}] {evt}");
    }

    private void HandleNodeExecuted(NodeExecutedEvent evt)
    {
        Console.WriteLine($"[{nameof(NodeExecutedEvent)}] {evt}");
    }

    private void HandleVerificationCompleted(VerificationCompletedEvent evt)
    {
        Console.WriteLine($"[{nameof(VerificationCompletedEvent)}] {evt}");
    }

    private void HandleRecoveryStrategyDetermined(RecoveryStrategyDeterminedEvent evt)
    {
        Console.WriteLine($"[{nameof(RecoveryStrategyDeterminedEvent)}] {evt}");
    }

    private void HandleRecoveryAction(RecoveryActionEvent evt)
    {
        Console.WriteLine($"[{nameof(RecoveryActionEvent)}] {evt}");
    }

    private void HandleWorkflowOutput(WorkflowOutputEvent evt)
    {
        Console.WriteLine($"\n>>> OUTPUT: {evt}\n");
    }
}
