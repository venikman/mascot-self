using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Models;
using AgentLmLocal.Services;

namespace AgentLmLocal.Workflow;

public sealed class WorkflowEventHandler
{
    private readonly string _workflowId;
    private readonly RunTracker _runTracker;
    private readonly Dictionary<string, int> _eventCounts;
    private readonly List<string> _outputs = new();

    public WorkflowEventHandler(string workflowId, RunTracker runTracker)
    {
        _workflowId = workflowId;
        _runTracker = runTracker;

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
        _runTracker.RecordEvent(_workflowId, eventType);

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
                Console.WriteLine($"[Workflow {_workflowId}][{eventType}] {evt}");
                break;
        }

        await Task.CompletedTask;
    }

    public Dictionary<string, int> GetEventCounts() => new(_eventCounts);
    public IReadOnlyList<string> GetOutputs() => _outputs.ToArray();

    private void HandlePlanGenerated(PlanGeneratedEvent evt)
    {
        Console.WriteLine($"[Workflow {_workflowId}][{nameof(PlanGeneratedEvent)}] {evt}");
    }

    private void HandleNodeExecuted(NodeExecutedEvent evt)
    {
        Console.WriteLine($"[Workflow {_workflowId}][{nameof(NodeExecutedEvent)}] {evt}");
    }

    private void HandleVerificationCompleted(VerificationCompletedEvent evt)
    {
        Console.WriteLine($"[Workflow {_workflowId}][{nameof(VerificationCompletedEvent)}] {evt}");
    }

    private void HandleRecoveryStrategyDetermined(RecoveryStrategyDeterminedEvent evt)
    {
        Console.WriteLine($"[Workflow {_workflowId}][{nameof(RecoveryStrategyDeterminedEvent)}] {evt}");
    }

    private void HandleRecoveryAction(RecoveryActionEvent evt)
    {
        Console.WriteLine($"[Workflow {_workflowId}][{nameof(RecoveryActionEvent)}] {evt}");
    }

    private void HandleWorkflowOutput(WorkflowOutputEvent evt)
    {
        var message = evt.ToString();
        Console.WriteLine($"\n[Workflow {_workflowId}] >>> OUTPUT: {message}\n");
        if (!string.IsNullOrWhiteSpace(message))
        {
            _outputs.Add(message);
            _runTracker.AppendOutput(_workflowId, message);
        }
    }
}
