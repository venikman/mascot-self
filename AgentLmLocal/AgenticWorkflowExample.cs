using System.Diagnostics;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Agents;
using AgentLmLocal;
using AgentLmLocal.Configuration;
using AgentLmLocal.Services;
using AgentLmLocal.Workflow;
using WorkflowCustomAgentExecutorsSample.Models;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace WorkflowCustomAgentExecutorsSample;

/// <summary>
/// Example demonstrating multi-agent coordination using the agentic workflow pattern.
///
/// This example showcases how the following agents work together:
/// 1. PlannerAgent: Decomposes tasks into multi-step DAGs
/// 2. ExecutorAgent: Implements planned steps by invoking tools/APIs
/// 3. VerifierAgent: Evaluates execution quality (LLM-as-a-judge)
/// 4. RecoveryHandlerAgent: Manages exceptions and failures
/// 5. RetrieverAgent: Provides external knowledge when needed
///
/// The agents coordinate through a workflow that:
/// - Plans complex tasks
/// - Executes steps with verification
/// - Handles failures with recovery strategies
/// - Retrieves knowledge as needed
///
/// Based on research from: https://www.emergentmind.com/research/5ec25fafbefce83f72059ba5
/// </summary>
public class AgenticWorkflowExample
{
    private readonly ILogger<AgenticWorkflowExample> _logger;
    private readonly AgentInstrumentation _telemetry;
    private readonly AgentConfiguration _config;
    private readonly AgentFactory _agentFactory;
    private readonly LlmService _llmService;
    private readonly IChatClient _chatClient;

    public AgenticWorkflowExample(
        ILogger<AgenticWorkflowExample> logger,
        AgentInstrumentation telemetry,
        AgentConfiguration config,
        AgentFactory agentFactory,
        LlmService llmService,
        IChatClient chatClient)
    {
        _logger = logger;
        _telemetry = telemetry;
        _config = config;
        _agentFactory = agentFactory;
        _llmService = llmService;
        _chatClient = chatClient;
    }
    
    public async Task RunExample()
    {
        var workflowId = Guid.NewGuid().ToString("N");
        using var activity = AgentInstrumentation.ActivitySource.StartActivity("AgenticWorkflowExample.RunExample");
        activity?.AddBaggage("workflow.id", workflowId);
        
        _logger.LogInformation("Starting Agentic Workflow Example with telemetry integration");
        // Minimal telemetry; no active agent tracking
        
        Console.WriteLine("""
            === Agentic Workflow Example ===
            This example demonstrates multi-agent coordination with:
            • Planner: Task decomposition into DAGs
            • Executor: Step-by-step execution
            • Verifier: Quality evaluation
            • Recovery: Error handling
            • Telemetry: Comprehensive monitoring and observability
            """);

        _logger.LogInformation("Configuring agents with endpoint: {Endpoint}, model: {ModelId}",
            _config.LmStudioEndpoint, _config.ModelId);

        // Create agents with telemetry instrumentation using factory
        _logger.LogInformation("Creating agent instances with telemetry integration");

        var planner = new PlannerAgent("Planner", _agentFactory, _llmService,
            new Microsoft.Extensions.Logging.Logger<PlannerAgent>(new Microsoft.Extensions.Logging.LoggerFactory()),
            _telemetry);

        var executor = new ExecutorAgent("Executor", _agentFactory, _llmService,
            new Microsoft.Extensions.Logging.Logger<ExecutorAgent>(new Microsoft.Extensions.Logging.LoggerFactory()),
            _telemetry)
        {
            VerificationInterval = 2 // Verify every 2 nodes
        };

        var verifier = new VerifierAgent("Verifier", _agentFactory, _llmService,
            new Microsoft.Extensions.Logging.Logger<VerifierAgent>(new Microsoft.Extensions.Logging.LoggerFactory()),
            _telemetry)
        {
            MinimumQualityScore = _config.MinimumRating,
            EnableSpeculativeExecution = true
        };

        var recoveryHandler = new RecoveryHandlerAgent("RecoveryHandler", _agentFactory, _llmService,
            new Microsoft.Extensions.Logging.Logger<RecoveryHandlerAgent>(new Microsoft.Extensions.Logging.LoggerFactory()),
            _telemetry)
        {
            MaxRetries = _config.MaxAttempts,
            EnableStateSnapshots = true
        };

        // Minimal telemetry; no active agent tracking

        var workflow = new Microsoft.Agents.AI.Workflows.WorkflowBuilder(planner)
            .AddEdge(planner, executor)
            .AddEdge(executor, verifier)
            .AddEdge(verifier, recoveryHandler)
            .AddEdge(recoveryHandler, executor)
            .WithOutputFrom(verifier)
            .Build();

        activity?.SetTag("workflow.id", workflowId);
        activity?.SetTag("workflow.agents.count", 4);
        activity?.SetTag("workflow.model", modelId);
        activity?.SetTag("workflow.endpoint", endpoint);

        // Example 1: Complex task that requires planning and execution
        Console.WriteLine("\n--- Example 1: Complex Multi-Step Task ---");
        _logger.LogInformation("Running Example 1: Complex Multi-Step Task");
        await RunWorkflowExample(
            workflow,
            "Create a comprehensive marketing campaign for a new AI-powered productivity app, " +
            "including market research, target audience analysis, content strategy, and launch timeline.");

        Console.WriteLine("\n\n--- Example 2: Task with Knowledge Retrieval ---");
        _logger.LogInformation("Running Example 2: Task with Knowledge Retrieval");
        await RunWorkflowExample(
            workflow,
            "Research and summarize the latest trends in quantum computing applications for 2024, " +
            "then create a technical presentation outline.");

        // Minimal telemetry; no active agent tracking
        _logger.LogInformation("Agentic Workflow Example completed successfully");
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        Console.WriteLine("\n\n=== Agentic Workflow Example Complete ===");
    }

    private async Task RunWorkflowExample(Microsoft.Agents.AI.Workflows.Workflow workflow, string task)
    {
        using var activity = AgentInstrumentation.ActivitySource.StartActivity("AgenticWorkflowExample.RunWorkflowExample");
        
        _logger.LogInformation("Starting workflow execution for task: {Task}", task);
        activity?.SetTag("workflow.task", task);
        activity?.SetTag("workflow.start_time", DateTimeOffset.UtcNow);

        Console.WriteLine($"\nTask: {task}\n");
        Console.WriteLine("Executing workflow...\n");

        // Create event handler
        var eventHandler = new WorkflowEventHandler(_logger, _telemetry);
        eventHandler.SetCurrentActivity(activity);

        {
            await using Microsoft.Agents.AI.Workflows.StreamingRun run = await Microsoft.Agents.AI.Workflows.InProcessExecution.StreamAsync(workflow, input: task);
            await foreach (Microsoft.Agents.AI.Workflows.WorkflowEvent evt in run.WatchStreamAsync())
            {
                await eventHandler.HandleEventAsync(evt);
            }

            // Log workflow completion metrics
            var eventCounts = eventHandler.GetEventCounts();
            activity?.SetTag("workflow.completion_time", DateTimeOffset.UtcNow);
            activity?.SetTag("workflow.events.total", eventCounts.Values.Sum());
            activity?.SetTag("workflow.events.plan_generated", eventCounts.GetValueOrDefault(nameof(PlanGeneratedEvent), 0));
            activity?.SetTag("workflow.events.nodes_executed", eventCounts.GetValueOrDefault(nameof(NodeExecutedEvent), 0));
            activity?.SetTag("workflow.events.verifications", eventCounts.GetValueOrDefault(nameof(VerificationCompletedEvent), 0));
            activity?.SetTag("workflow.events.recoveries", eventCounts.GetValueOrDefault(nameof(RecoveryStrategyDeterminedEvent), 0));
            activity?.SetTag("workflow.events.outputs", eventCounts.GetValueOrDefault(nameof(WorkflowOutputEvent), 0));

            _logger.LogInformation("Workflow completed successfully. Events: {EventSummary}",
                string.Join(", ", eventCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}")));

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }
}
