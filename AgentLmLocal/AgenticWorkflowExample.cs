using System;
using System.Linq;
using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Agents;
using AgentLmLocal.Configuration;
using AgentLmLocal.Services;
using AgentLmLocal.Workflow;
using Microsoft.Extensions.Logging;

namespace WorkflowCustomAgentExecutorsSample;

public class AgenticWorkflowExample
{
    private readonly AgentConfiguration _config;
    private readonly AgentFactory _agentFactory;
    private readonly LlmService _llmService;
    private readonly RunTracker _runTracker;
    private readonly ILogger<AgenticWorkflowExample> _logger;

    public AgenticWorkflowExample(
        AgentConfiguration config,
        AgentFactory agentFactory,
        LlmService llmService,
        RunTracker runTracker,
        ILogger<AgenticWorkflowExample> logger)
    {
        _config = config;
        _agentFactory = agentFactory;
        _llmService = llmService;
        _runTracker = runTracker;
        _logger = logger;
    }

    public async Task RunWorkflowAsync(string workflowId, string task, CancellationToken cancellationToken = default)
    {
        var workflow = BuildWorkflow();
        await RunWorkflowExample(workflowId, workflow, task, cancellationToken);
    }

    private Workflow BuildWorkflow()
    {
        var planner = new PlannerAgent("Planner", _agentFactory, _llmService);

        var executor = new ExecutorAgent("Executor", _agentFactory, _llmService)
        {
            VerificationInterval = 2
        };

        var verifier = new VerifierAgent("Verifier", _agentFactory, _llmService)
        {
            MinimumQualityScore = _config.MinimumRating,
            EnableSpeculativeExecution = true
        };

        var recoveryHandler = new RecoveryHandlerAgent("RecoveryHandler", _agentFactory, _llmService)
        {
            MaxRetries = _config.MaxAttempts,
            EnableStateSnapshots = true
        };

        return new WorkflowBuilder(planner)
            .AddEdge(planner, executor)
            .AddEdge(executor, verifier)
            .AddEdge(verifier, recoveryHandler)
            .AddEdge(recoveryHandler, executor)
            .WithOutputFrom(verifier)
            .Build();
    }

    private async Task RunWorkflowExample(string workflowId, Workflow workflow, string task, CancellationToken cancellationToken)
    {
        Console.WriteLine("Workflow {0} starting. Task: {1}", workflowId, task);
        _logger.LogInformation("Workflow {WorkflowId} executing task \"{Task}\"", workflowId, task);

        var eventHandler = new WorkflowEventHandler(workflowId, _runTracker);

        try
        {
            await using var run = await InProcessExecution.StreamAsync(workflow, input: task, cancellationToken: cancellationToken);
            await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
            {
                await eventHandler.HandleEventAsync(evt);
            }

            var eventCounts = eventHandler.GetEventCounts();

            _runTracker.MarkCompleted(workflowId);

            var summary = string.Join(", ", eventCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));

            Console.WriteLine("Workflow {0} completed successfully. Events: {1}", workflowId, summary);
            _logger.LogInformation("Workflow {WorkflowId} finished. {Summary}", workflowId, summary);
        }
        catch (Exception ex)
        {
            _runTracker.MarkFailed(workflowId, ex);
            Console.WriteLine("Workflow {0} failed: {1}", workflowId, ex.Message);
            _logger.LogError(ex, "Workflow {WorkflowId} failed", workflowId);
        }
    }
}
