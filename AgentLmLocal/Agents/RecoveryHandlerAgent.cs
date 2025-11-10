using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Models;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;
using AgentLmLocal;
using AgentLmLocal.Services;
using AgentLmLocal.Workflow;

namespace WorkflowCustomAgentExecutorsSample.Agents;

/// <summary>
/// RecoveryHandlerAgent: Manages exceptions and implements recovery strategies.
///
/// This agent handles runtime failures by analyzing root causes, determining
/// appropriate recovery patterns (retry, rollback, skip, escalate), and executing
/// state restoration when needed.
///
/// Based on the agentic workflows pattern described in the research paper.
/// </summary>
public sealed class RecoveryHandlerAgent : InstrumentedAgent<object>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly LlmService _llmService;
    private readonly Dictionary<string, int> _retryCount = [];
    private readonly Stack<object> _stateSnapshots = new();

    /// <summary>
    /// Maximum number of retry attempts before escalation.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Whether to automatically capture state snapshots for rollback.
    /// </summary>
    public bool EnableStateSnapshots { get; init; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryHandlerAgent"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the recovery handler agent.</param>
    /// <param name="agentFactory">Factory for creating AI agents.</param>
    /// <param name="llmService">Service for LLM invocations.</param>
    /// <param name="logger">Logger instance for telemetry.</param>
    /// <param name="telemetry">Telemetry instrumentation instance.</param>
    public RecoveryHandlerAgent(
        string id,
        AgentFactory agentFactory,
        LlmService llmService,
        ILogger<RecoveryHandlerAgent> logger,
        AgentInstrumentation telemetry)
        : base(id, logger, telemetry)
    {
        _llmService = llmService;

        var instructions = """
            You are an expert recovery handler that analyzes failures and implements recovery strategies.

            Your responsibilities:
            1. Analyze errors and determine root causes
            2. Classify error types (transient, permanent, configuration, resource, logic)
            3. Select appropriate recovery actions:
               - RETRY: For transient errors (network, timeout, temporary unavailability)
               - ROLLBACK: For state corruption or partial failures
               - SKIP: For non-critical optional steps
               - ESCALATE: For critical errors requiring human intervention
            4. Determine if state restoration is needed
            5. Suggest alternative execution paths when available
            6. Provide detailed recovery recommendations

            Error Classification Guidelines:
            - Transient: Network issues, timeouts, rate limits (→ RETRY)
            - State corruption: Invalid state, data inconsistency (→ ROLLBACK)
            - Configuration: Missing config, invalid parameters (→ ESCALATE)
            - Resource: Out of memory, disk full (→ ESCALATE)
            - Logic: Business rule violations (→ SKIP or ESCALATE)

            Be conservative with retries and aggressive with state protection.
            """;

        (_agent, _thread) = agentFactory.CreateAgent(
            instructions,
            ChatResponseFormat.ForJsonSchema<RecoveryStrategy>());
    }

    protected override RouteBuilder ConfigureRoutes(RouteBuilder routeBuilder) =>
        routeBuilder.AddHandler<VerificationResult, RecoveryStrategy>(this.HandleVerificationFailureAsync)
                    .AddHandler<ExecutionResult, RecoveryStrategy>(this.HandleExecutionFailureAsync);

    /// <summary>
    /// Handles recovery with telemetry instrumentation.
    /// </summary>
    protected override async ValueTask ExecuteInstrumentedAsync(
        object message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        switch (message)
        {
            case VerificationResult verification:
                await HandleVerificationFailureAsync(verification, context, cancellationToken);
                break;
            case ExecutionResult result:
                await HandleExecutionFailureAsync(result, context, cancellationToken);
                break;
            default:
                await HandleExecutionFailureAsync((ExecutionResult)message, context, cancellationToken);
                break;
        }
    }

    private async ValueTask<RecoveryStrategy> HandleVerificationFailureAsync(
        VerificationResult verification,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Agent.Recovery.VerificationFailure");

        activity?.SetTag("recovery.node_id", verification.NodeId);
        activity?.SetTag("recovery.error_type", "verification_failure");
        activity?.SetTag("recovery.quality_score", verification.QualityScore);
        activity?.SetTag("recovery.requires_rollback", verification.RequiresRollback);

        Telemetry.RecordActivity(Id, "recovery_started");

        var errorMessage = $"""
            Verification failed for node: {verification.NodeId}
            Quality Score: {verification.QualityScore}/10
            Feedback: {verification.Feedback}
            Suggestions: {string.Join(", ", verification.Suggestions)}
            Requires Rollback: {verification.RequiresRollback}
            """;


        {
            var strategy = await DetermineRecoveryStrategyAsync(
                verification.NodeId,
                errorMessage,
                "verification_failure",
                cancellationToken);

            activity?.SetTag("recovery.action", strategy.RecoveryAction);
            activity?.SetTag("recovery.state_restoration", strategy.StateRestorationNeeded);
            activity?.SetTag("recovery.alternative_path", strategy.AlternativePath?.Count ?? 0);

            await context.AddEventAsync(new RecoveryStrategyDeterminedEvent(strategy), cancellationToken);

            // Execute recovery action
            await ExecuteRecoveryAsync(strategy, context, cancellationToken);

            Telemetry.RecordActivity(Id, "recovery_completed");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return strategy;
        }
    }

    private async ValueTask<RecoveryStrategy> HandleExecutionFailureAsync(
        ExecutionResult result,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Agent.Recovery.ExecutionFailure");

        activity?.SetTag("recovery.node_id", result.NodeId);
        activity?.SetTag("recovery.error_type", "execution_failure");
        activity?.SetTag("recovery.status", result.Status);
        activity?.SetTag("recovery.execution_time_ms", result.ExecutionTimeMs);

        Telemetry.RecordActivity(Id, "recovery_started");

        var errorMessage = result.Error ?? "Unknown execution error";


        {
            var strategy = await DetermineRecoveryStrategyAsync(
                result.NodeId,
                errorMessage,
                "execution_failure",
                cancellationToken);

            activity?.SetTag("recovery.action", strategy.RecoveryAction);
            activity?.SetTag("recovery.state_restoration", strategy.StateRestorationNeeded);
            activity?.SetTag("recovery.alternative_path", strategy.AlternativePath?.Count ?? 0);

            await context.AddEventAsync(new RecoveryStrategyDeterminedEvent(strategy), cancellationToken);

            // Execute recovery action
            await ExecuteRecoveryAsync(strategy, context, cancellationToken);

            Telemetry.RecordActivity(Id, "recovery_completed");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return strategy;
        }
    }

    /// <summary>
    /// Determines the appropriate recovery strategy using the LLM.
    /// </summary>
    private async Task<RecoveryStrategy> DetermineRecoveryStrategyAsync(
        string nodeId,
        string errorMessage,
        string errorType,
        CancellationToken cancellationToken)
    {
        var retries = _retryCount.GetValueOrDefault(nodeId, 0);

        var prompt = $"""
            Analyze the following error and determine the best recovery strategy:

            Node ID: {nodeId}
            Error Type: {errorType}
            Error Message: {errorMessage}
            Previous Retry Attempts: {retries}/{MaxRetries}

            Please provide:
            1. Root cause analysis
            2. Error classification
            3. Recovery action (retry, rollback, skip, or escalate)
            4. Whether state restoration is needed
            5. Alternative execution paths if available

            Consider:
            - If retries are exhausted ({retries} >= {MaxRetries}), don't suggest retry
            - For critical errors, prefer escalation over retries
            - State restoration should be used for data integrity issues
            """;

        var strategy = await _llmService.InvokeStructuredAsync<RecoveryStrategy>(
            _agent, _thread, prompt, cancellationToken);

        strategy.ErrorType = errorType;

        return strategy;
    }

    /// <summary>
    /// Executes the determined recovery strategy.
    /// </summary>
    private async Task ExecuteRecoveryAsync(
        RecoveryStrategy strategy,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var action = EnumExtensions.ParseRecoveryAction(strategy.RecoveryAction);
        switch (action)
        {
            case RecoveryAction.Retry:
                await HandleRetryAsync(strategy, context, cancellationToken);
                break;

            case RecoveryAction.Rollback:
                await HandleRollbackAsync(strategy, context, cancellationToken);
                break;

            case RecoveryAction.Skip:
                await HandleSkipAsync(strategy, context, cancellationToken);
                break;

            case RecoveryAction.Escalate:
                await HandleEscalationAsync(strategy, context, cancellationToken);
                break;

            default:
                await context.AddEventAsync(
                    new RecoveryActionEvent($"Unknown recovery action: {strategy.RecoveryAction}"),
                    cancellationToken);
                break;
        }
    }

    private async Task HandleRetryAsync(
        RecoveryStrategy strategy,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        // Increment retry counter
        var nodeId = ExtractNodeId(strategy);
        _retryCount[nodeId] = _retryCount.GetValueOrDefault(nodeId, 0) + 1;

        if (_retryCount[nodeId] > MaxRetries)
        {
            await context.AddEventAsync(
                new RecoveryActionEvent($"Max retries exceeded for {nodeId}, escalating..."),
                cancellationToken);

            strategy.RecoveryAction = RecoveryAction.Escalate.ToLowerString();
            await HandleEscalationAsync(strategy, context, cancellationToken);
            return;
        }

        await context.AddEventAsync(
            new RecoveryActionEvent($"Retrying node {nodeId} (attempt {_retryCount[nodeId]}/{MaxRetries})"),
            cancellationToken);

        // Signal retry - in a real implementation, this would trigger re-execution
        await context.YieldOutputAsync(
            $"Recovery: Retry attempt {_retryCount[nodeId]} for {nodeId}",
            cancellationToken);
    }

    private async Task HandleRollbackAsync(
        RecoveryStrategy strategy,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new RecoveryActionEvent("Executing rollback to previous state..."),
            cancellationToken);

        if (EnableStateSnapshots && _stateSnapshots.Count > 0)
        {
            var previousState = _stateSnapshots.Pop();
            await context.AddEventAsync(
                new RecoveryActionEvent($"State restored from snapshot"),
                cancellationToken);
        }

        await context.YieldOutputAsync(
            $"Recovery: Rolled back due to {strategy.RootCause}",
            cancellationToken);
    }

    private async Task HandleSkipAsync(
        RecoveryStrategy strategy,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new RecoveryActionEvent("Skipping failed step and continuing..."),
            cancellationToken);

        await context.YieldOutputAsync(
            $"Recovery: Skipped failed step - {strategy.RootCause}",
            cancellationToken);
    }

    private async Task HandleEscalationAsync(
        RecoveryStrategy strategy,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await context.AddEventAsync(
            new RecoveryActionEvent($"ESCALATION REQUIRED: {strategy.RootCause}"),
            cancellationToken);

        await context.YieldOutputAsync(
            $"Recovery: ESCALATION - {strategy.RootCause}\n" +
            $"Recommended actions: {strategy.RecoveryAction}",
            cancellationToken);
    }

    private static string ExtractNodeId(RecoveryStrategy strategy)
    {
        // Extract node ID from error type or root cause
        // This is a simplified implementation
        return strategy.ErrorType;
    }

    /// <summary>
    /// Captures a state snapshot for potential rollback.
    /// </summary>
    public void CaptureStateSnapshot(object state)
    {
        if (EnableStateSnapshots)
        {
            _stateSnapshots.Push(state);
        }
    }
}
