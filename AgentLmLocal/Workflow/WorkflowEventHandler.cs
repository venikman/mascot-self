using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Models;

namespace AgentLmLocal.Workflow;

/// <summary>
/// Handles workflow events with logging.
/// </summary>
public sealed class WorkflowEventHandler
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, int> _eventCounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowEventHandler"/> class.
    /// </summary>
    public WorkflowEventHandler(ILogger logger)
    {
        _logger = logger;
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

    /// <summary>
    /// Handles a workflow event.
    /// </summary>
    public async Task HandleEventAsync(WorkflowEvent evt)
    {
        var eventType = evt.GetType().Name;
        _eventCounts[eventType] = _eventCounts.GetValueOrDefault(eventType, 0) + 1;

        _logger.LogDebug("Workflow event: {EventType} - {EventDetails}", eventType, evt);

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

    /// <summary>
    /// Gets the event count summary.
    /// </summary>
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
