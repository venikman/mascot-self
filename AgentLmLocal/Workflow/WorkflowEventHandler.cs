using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Models;

namespace AgentLmLocal.Workflow;

/// <summary>
/// Handles workflow events with telemetry and logging.
/// </summary>
public sealed class WorkflowEventHandler
{
    private readonly ILogger _logger;
    private readonly AgentInstrumentation _telemetry;
    private readonly Dictionary<string, int> _eventCounts;
    private Activity? _currentActivity;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowEventHandler"/> class.
    /// </summary>
    public WorkflowEventHandler(ILogger logger, AgentInstrumentation telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
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
    /// Sets the current activity for tagging.
    /// </summary>
    public void SetCurrentActivity(Activity? activity)
    {
        _currentActivity = activity;
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
        _currentActivity?.SetTag("workflow.plan_generated", true);
        _telemetry.RecordActivity("Workflow", "plan_generated");
        Console.WriteLine($"[{nameof(PlanGeneratedEvent)}] {evt}");
    }

    private void HandleNodeExecuted(NodeExecutedEvent evt)
    {
        var nodeResult = evt.GetType().GetProperty("Data")?.GetValue(evt) as ExecutionResult;
        if (nodeResult != null)
        {
            _currentActivity?.SetTag($"workflow.node_{nodeResult.NodeId}_executed", true);
            _telemetry.RecordActivity("Workflow", $"node_executed_{nodeResult.Status}");
        }
        Console.WriteLine($"[{nameof(NodeExecutedEvent)}] {evt}");
    }

    private void HandleVerificationCompleted(VerificationCompletedEvent evt)
    {
        var verificationResult = evt.GetType().GetProperty("Data")?.GetValue(evt) as VerificationResult;
        if (verificationResult != null)
        {
            _currentActivity?.SetTag("workflow.verification_completed", true);
            _currentActivity?.SetTag("workflow.verification_score", verificationResult.QualityScore);
            _telemetry.RecordActivity("Workflow", verificationResult.Passed ? "verification_passed" : "verification_failed");
        }
        Console.WriteLine($"[{nameof(VerificationCompletedEvent)}] {evt}");
    }

    private void HandleRecoveryStrategyDetermined(RecoveryStrategyDeterminedEvent evt)
    {
        var recoveryStrategy = evt.GetType().GetProperty("Data")?.GetValue(evt) as RecoveryStrategy;
        if (recoveryStrategy != null)
        {
            _currentActivity?.SetTag("workflow.recovery_strategy_determined", true);
            _currentActivity?.SetTag("workflow.recovery_action", recoveryStrategy.RecoveryAction);
            _telemetry.RecordActivity("Workflow", $"recovery_{recoveryStrategy.RecoveryAction}");
        }
        Console.WriteLine($"[{nameof(RecoveryStrategyDeterminedEvent)}] {evt}");
    }

    private void HandleRecoveryAction(RecoveryActionEvent evt)
    {
        _currentActivity?.SetTag("workflow.recovery_action_taken", true);
        _telemetry.RecordActivity("Workflow", "recovery_action");
        Console.WriteLine($"[{nameof(RecoveryActionEvent)}] {evt}");
    }

    private void HandleWorkflowOutput(WorkflowOutputEvent evt)
    {
        _currentActivity?.SetTag("workflow.output_generated", true);
        _currentActivity?.SetTag("workflow.output_length", evt.ToString()?.Length ?? 0);
        _telemetry.RecordActivity("Workflow", "output_generated");
        Console.WriteLine($"\n>>> OUTPUT: {evt}\n");
    }
}
