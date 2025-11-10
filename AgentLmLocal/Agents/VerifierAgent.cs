using System.Text.Json;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Models;
using AgentLmLocal;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace WorkflowCustomAgentExecutorsSample.Agents;

/// <summary>
/// VerifierAgent: Acts as "LLM-as-a-judge" to evaluate workflow step outputs.
///
/// This agent gates workflow advancement by evaluating the quality and correctness
/// of execution results. It uses selective verification and supports speculative
/// execution with rollback capabilities when quality thresholds aren't met.
///
/// Based on the agentic workflows pattern described in the research paper.
/// </summary>
public sealed class VerifierAgent : InstrumentedAgent<object>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    /// <summary>
    /// Minimum quality score (1-10) required to pass verification.
    /// </summary>
    public int MinimumQualityScore { get; init; } = 7;

    /// <summary>
    /// Whether to enable speculative execution (continue on minor issues).
    /// </summary>
    public bool EnableSpeculativeExecution { get; init; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="VerifierAgent"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the verifier agent.</param>
    /// <param name="chatClient">The chat client to use for the AI agent.</param>
    /// <param name="logger">Logger instance for telemetry.</param>
    /// <param name="telemetry">Telemetry instrumentation instance.</param>
    public VerifierAgent(string id, IChatClient chatClient, ILogger<VerifierAgent> logger, AgentInstrumentation telemetry) 
        : base(id, logger, telemetry)
    {
        ChatClientAgentOptions agentOptions = new(
            instructions: """
            You are an expert verification agent that evaluates the quality and correctness of workflow execution results.

            Your responsibilities:
            1. Evaluate execution results for quality, correctness, and completeness
            2. Assign quality scores from 1-10 based on objective criteria
            3. Provide constructive feedback on issues found
            4. Suggest specific improvements or corrections
            5. Determine if rollback is needed for critical failures
            6. Enable speculative execution for minor issues that can be corrected later

            Evaluation criteria:
            - Correctness: Does the output match expected results?
            - Completeness: Are all required elements present?
            - Quality: Is the output well-formed and usable?
            - Safety: Does the output pose any risks?
            - Efficiency: Was the execution reasonably efficient?

            Be objective and thorough in your assessments. Provide actionable feedback.
            """)
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<VerificationResult>()
            }
        };

        this._agent = new ChatClientAgent(chatClient, agentOptions);
        this._thread = this._agent.GetNewThread();
    }

    protected override Microsoft.Agents.AI.Workflows.RouteBuilder ConfigureRoutes(Microsoft.Agents.AI.Workflows.RouteBuilder routeBuilder) =>
        routeBuilder.AddHandler<List<ExecutionResult>, VerificationResult>(this.VerifyMultipleAsync)
                    .AddHandler<ExecutionResult, VerificationResult>(this.VerifySingleAsync);

    /// <summary>
    /// Verifies execution results with telemetry instrumentation.
    /// </summary>
    protected override async ValueTask ExecuteInstrumentedAsync(
        object message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        switch (message)
        {
            case List<ExecutionResult> results:
                await VerifyMultipleAsync(results, context, cancellationToken);
                break;
            case ExecutionResult result:
                await VerifySingleAsync(result, context, cancellationToken);
                break;
            default:
                // Be less defensive: assume single result message
                await VerifySingleAsync((ExecutionResult)message, context, cancellationToken);
                break;
        }
    }

    private async ValueTask<VerificationResult> VerifyMultipleAsync(
        List<ExecutionResult> results,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Agent.Verifier.MultiVerification");
        
        activity?.SetTag("verification.results.count", results.Count);
        activity?.SetTag("verification.minimum_score", MinimumQualityScore);
        activity?.SetTag("verification.speculative_enabled", EnableSpeculativeExecution);
        
        Telemetry.RecordActivity(Id, "verification_started");
        
        var verificationResults = new List<VerificationResult>();
        var passedVerifications = 0;
        var failedVerifications = 0;

        
        {
            foreach (var result in results)
            {
                var verification = await PerformVerificationAsync(result, cancellationToken);
                verificationResults.Add(verification);

                if (verification.Passed)
                {
                    passedVerifications++;
                }
                else
                {
                    failedVerifications++;
                }

                await context.AddEventAsync(new VerificationCompletedEvent(verification), cancellationToken);

                // If critical failure detected, handle immediately
                if (!verification.Passed && verification.RequiresRollback)
                {
                    activity?.SetTag("verification.critical_failure", true);
                    activity?.SetTag("verification.failed_node", result.NodeId);
                    await context.SendMessageAsync(verification, cancellationToken: cancellationToken);
                    return verification; // Return the critical failure result
                }
            }

            // Find the lowest scoring result
            var lowestScore = verificationResults.MinBy(v => v.QualityScore);
            if (lowestScore != null && !lowestScore.Passed)
            {
                activity?.SetTag("verification.lowest_score", lowestScore.QualityScore);
                await context.SendMessageAsync(lowestScore, cancellationToken: cancellationToken);
                return lowestScore; // Return the failed result
            }

            activity?.SetTag("verification.passed_count", passedVerifications);
            activity?.SetTag("verification.failed_count", failedVerifications);
            
            Telemetry.RecordActivity(Id, "verification_completed");

            // All passed - send success signal
            await context.YieldOutputAsync(
                $"Verification passed: {verificationResults.Count} results verified successfully.",
                cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return verificationResults.FirstOrDefault() ?? new VerificationResult 
            { 
                NodeId = "multi_verification", 
                Passed = true, 
                QualityScore = 10,
                Feedback = "All verifications passed successfully"
            };
        }
    }

    private async ValueTask<VerificationResult> VerifySingleAsync(
        ExecutionResult result,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Agent.Verifier.SingleVerification");
        
        activity?.SetTag("verification.node_id", result.NodeId);
        activity?.SetTag("verification.minimum_score", MinimumQualityScore);
        activity?.SetTag("verification.speculative_enabled", EnableSpeculativeExecution);
        
        Telemetry.RecordActivity(Id, "verification_started");

        
        {
            var verification = await PerformVerificationAsync(result, cancellationToken);

            activity?.SetTag("verification.quality_score", verification.QualityScore);
            activity?.SetTag("verification.passed", verification.Passed);
            activity?.SetTag("verification.requires_rollback", verification.RequiresRollback);

            await context.AddEventAsync(new VerificationCompletedEvent(verification), cancellationToken);

            if (!verification.Passed)
            {
                Telemetry.RecordActivity(Id, "verification_failed");
                // Send to recovery handler
                await context.SendMessageAsync(verification, cancellationToken: cancellationToken);
            }
            else
            {
                Telemetry.RecordActivity(Id, "verification_completed");
                await context.YieldOutputAsync(
                    $"Verification passed for node {result.NodeId} (score: {verification.QualityScore}/10)",
                    cancellationToken);
            }

            activity?.SetStatus(verification.Passed ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
            return verification;
        }
    }

    /// <summary>
    /// Performs the actual verification using the LLM.
    /// </summary>
    private async Task<VerificationResult> PerformVerificationAsync(
        ExecutionResult result,
        CancellationToken cancellationToken)
    {
        // Handle execution failures specially
        if (result.Status == "failure")
        {
            return new VerificationResult
            {
                NodeId = result.NodeId,
                Passed = false,
                QualityScore = 0,
                Feedback = $"Execution failed: {result.Error}",
                Suggestions = ["Fix the underlying execution error", "Implement error handling"],
                RequiresRollback = true
            };
        }

        var prompt = $"""
            Please verify the following execution result:

            Node ID: {result.NodeId}
            Status: {result.Status}
            Output: {result.Output}
            Execution Time: {result.ExecutionTimeMs}ms
            Metadata: {JsonSerializer.Serialize(result.Metadata)}

            Evaluate the quality and correctness of this result. Provide:
            1. A quality score from 1-10
            2. Whether it passes verification
            3. Constructive feedback
            4. Specific suggestions for improvement
            5. Whether rollback is required

            Use the following thresholds:
            - Score >= {MinimumQualityScore}: Pass
            - Score < {MinimumQualityScore}: Fail (may allow speculative execution if issues are minor)
            - Critical errors: Require rollback
            """;

        var response = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

        var verification = JsonSerializer.Deserialize<VerificationResult>(response.Text)!;

        // Apply business rules
        if (verification.QualityScore < MinimumQualityScore)
        {
            verification.Passed = false;

            // Allow speculative execution for minor issues
            if (EnableSpeculativeExecution && verification.QualityScore >= MinimumQualityScore - 2)
            {
                verification.Passed = true; // Allow to continue but flag for review
                verification.Suggestions.Add("Flagged for review in speculative execution mode");
            }
        }
        else
        {
            verification.Passed = true;
        }

        return verification;
    }
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
