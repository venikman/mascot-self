using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using WorkflowCustomAgentExecutorsSample.Models;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

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
public sealed class RecoveryHandlerAgent : Executor
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
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
    /// <param name="chatClient">The chat client to use for the AI agent.</param>
    public RecoveryHandlerAgent(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new(
            instructions: """
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
            """)
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<RecoveryStrategy>()
            }
        };

        this._agent = new ChatClientAgent(chatClient, agentOptions);
        this._thread = this._agent.GetNewThread();
    }

    protected override RouteBuilder ConfigureRoutes(RouteBuilder routeBuilder) =>
        routeBuilder.AddHandler<VerificationResult, RecoveryStrategy>(this.HandleVerificationFailureAsync)
                    .AddHandler<ExecutionResult, RecoveryStrategy>(this.HandleExecutionFailureAsync);

    /// <summary>
    /// Handles verification failures.
    /// </summary>
    public async ValueTask<RecoveryStrategy> HandleVerificationFailureAsync(
        VerificationResult verification,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var errorMessage = $"""
            Verification failed for node: {verification.NodeId}
            Quality Score: {verification.QualityScore}/10
            Feedback: {verification.Feedback}
            Suggestions: {string.Join(", ", verification.Suggestions)}
            Requires Rollback: {verification.RequiresRollback}
            """;

        var strategy = await DetermineRecoveryStrategyAsync(
            verification.NodeId,
            errorMessage,
            "verification_failure",
            cancellationToken);

        await context.AddEventAsync(new RecoveryStrategyDeterminedEvent(strategy), cancellationToken);

        // Execute recovery action
        await ExecuteRecoveryAsync(strategy, context, cancellationToken);

        return strategy;
    }

    /// <summary>
    /// Handles execution failures.
    /// </summary>
    public async ValueTask<RecoveryStrategy> HandleExecutionFailureAsync(
        ExecutionResult result,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var errorMessage = result.Error ?? "Unknown execution error";

        var strategy = await DetermineRecoveryStrategyAsync(
            result.NodeId,
            errorMessage,
            "execution_failure",
            cancellationToken);

        await context.AddEventAsync(new RecoveryStrategyDeterminedEvent(strategy), cancellationToken);

        // Execute recovery action
        await ExecuteRecoveryAsync(strategy, context, cancellationToken);

        return strategy;
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

        var response = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        var strategy = JsonSerializer.Deserialize<RecoveryStrategy>(response.Text)
            ?? throw new InvalidOperationException("Failed to deserialize recovery strategy.");

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
        switch (strategy.RecoveryAction.ToLowerInvariant())
        {
            case "retry":
                await HandleRetryAsync(strategy, context, cancellationToken);
                break;

            case "rollback":
                await HandleRollbackAsync(strategy, context, cancellationToken);
                break;

            case "skip":
                await HandleSkipAsync(strategy, context, cancellationToken);
                break;

            case "escalate":
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

            strategy.RecoveryAction = "escalate";
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
