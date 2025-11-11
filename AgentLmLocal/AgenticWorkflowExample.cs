using System;
using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Agents;
using AgentLmLocal.Configuration;
using AgentLmLocal.Services;
using AgentLmLocal.Workflow;

namespace WorkflowCustomAgentExecutorsSample;

public class AgenticWorkflowExample
{
    private readonly AgentConfiguration _config;
    private readonly AgentFactory _agentFactory;
    private readonly LlmService _llmService;

    public AgenticWorkflowExample(
        AgentConfiguration config,
        AgentFactory agentFactory,
        LlmService llmService)
    {
        _config = config;
        _agentFactory = agentFactory;
        _llmService = llmService;
    }
    
    public async Task RunWorkflowAsync(string task)
    {
        var workflow = BuildWorkflow();
        await RunWorkflowExample(workflow, task);
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

    private async Task RunWorkflowExample(Workflow workflow, string task)
    {
        Console.WriteLine("Starting workflow execution for task: {0}", task);

        var eventHandler = new WorkflowEventHandler();

        await using var run = await InProcessExecution.StreamAsync(workflow, input: task);
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            await eventHandler.HandleEventAsync(evt);
        }

        var eventCounts = eventHandler.GetEventCounts();

        Console.WriteLine("Workflow completed successfully. Events: {0}",
            string.Join(", ", eventCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
    }
}
