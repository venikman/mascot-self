using System.ClientModel;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using WorkflowCustomAgentExecutorsSample.Agents;
using AgentLmLocal;
using WorkflowCustomAgentExecutorsSample.Models;

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
    
    public AgenticWorkflowExample(ILogger<AgenticWorkflowExample> logger, AgentInstrumentation telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
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

        var endpoint = Environment.GetEnvironmentVariable("LMSTUDIO_ENDPOINT") ?? "http://localhost:1234/v1";
        var apiKey = Environment.GetEnvironmentVariable("LMSTUDIO_API_KEY") ?? "lm-studio";
        var modelId = Environment.GetEnvironmentVariable("LMSTUDIO_MODEL") ?? "openai/gpt-oss-20b";

        _logger.LogInformation("Configuring LM Studio client with endpoint: {Endpoint}, model: {ModelId}", endpoint, modelId);
        
        OpenAI.OpenAIClient lmStudioClient = new(
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            });

        var chatClient = lmStudioClient.GetChatClient(modelId).AsIChatClient();

        // Create agents with telemetry instrumentation
        _logger.LogInformation("Creating agent instances with telemetry integration");
        
        var planner = new PlannerAgent("Planner", chatClient, 
            new Microsoft.Extensions.Logging.Logger<PlannerAgent>(new Microsoft.Extensions.Logging.LoggerFactory()), 
            _telemetry);
            
        var executor = new ExecutorAgent("Executor", chatClient,
            new Microsoft.Extensions.Logging.Logger<ExecutorAgent>(new Microsoft.Extensions.Logging.LoggerFactory()), 
            _telemetry)
        {
            VerificationInterval = 2 // Verify every 2 nodes
        };
        
        var verifier = new VerifierAgent("Verifier", chatClient,
            new Microsoft.Extensions.Logging.Logger<VerifierAgent>(new Microsoft.Extensions.Logging.LoggerFactory()), 
            _telemetry)
        {
            MinimumQualityScore = 7,
            EnableSpeculativeExecution = true
        };
        
        var recoveryHandler = new RecoveryHandlerAgent("RecoveryHandler", chatClient,
            new Microsoft.Extensions.Logging.Logger<RecoveryHandlerAgent>(new Microsoft.Extensions.Logging.LoggerFactory()), 
            _telemetry)
        {
            MaxRetries = 3,
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

        var eventCounts = new Dictionary<string, int>
        {
            ["PlanGeneratedEvent"] = 0,
            ["NodeExecutedEvent"] = 0,
            ["VerificationCompletedEvent"] = 0,
            ["RecoveryStrategyDeterminedEvent"] = 0,
            ["RecoveryActionEvent"] = 0,
            ["WorkflowOutputEvent"] = 0
        };

        
        {
            await using Microsoft.Agents.AI.Workflows.StreamingRun run = await Microsoft.Agents.AI.Workflows.InProcessExecution.StreamAsync(workflow, input: task);
            await foreach (Microsoft.Agents.AI.Workflows.WorkflowEvent evt in run.WatchStreamAsync())
            {
                var eventType = evt.GetType().Name;
                eventCounts[eventType] = eventCounts.GetValueOrDefault(eventType, 0) + 1;
                
                _logger.LogDebug("Workflow event: {EventType} - {EventDetails}", eventType, evt);
                
                switch (evt)
                {
                    case PlanGeneratedEvent planEvent:
                        activity?.SetTag("workflow.plan_generated", true);
                        _telemetry.RecordActivity("Workflow", "plan_generated");
                        Console.WriteLine($"[{eventType}] {evt}");
                        break;

                    case NodeExecutedEvent nodeEvent:
                        // NodeExecutedEvent contains ExecutionResult in its base data
                        var nodeResult = nodeEvent.GetType().GetProperty("Data")?.GetValue(nodeEvent) as ExecutionResult;
                        if (nodeResult != null)
                        {
                            activity?.SetTag($"workflow.node_{nodeResult.NodeId}_executed", true);
                            _telemetry.RecordActivity("Workflow", $"node_executed_{nodeResult.Status}");
                        }
                        Console.WriteLine($"[{eventType}] {evt}");
                        break;

                    case VerificationCompletedEvent verificationEvent:
                        // VerificationCompletedEvent contains VerificationResult in its base data
                        var verificationResult = verificationEvent.GetType().GetProperty("Data")?.GetValue(verificationEvent) as VerificationResult;
                        if (verificationResult != null)
                        {
                            activity?.SetTag("workflow.verification_completed", true);
                            activity?.SetTag($"workflow.verification_score", verificationResult.QualityScore);
                            _telemetry.RecordActivity("Workflow", verificationResult.Passed ? "verification_passed" : "verification_failed");
                        }
                        Console.WriteLine($"[{eventType}] {evt}");
                        break;

                    case RecoveryStrategyDeterminedEvent recoveryEvent:
                        // RecoveryStrategyDeterminedEvent contains RecoveryStrategy in its base data
                        var recoveryStrategy = recoveryEvent.GetType().GetProperty("Data")?.GetValue(recoveryEvent) as RecoveryStrategy;
                        if (recoveryStrategy != null)
                        {
                            activity?.SetTag("workflow.recovery_strategy_determined", true);
                            activity?.SetTag($"workflow.recovery_action", recoveryStrategy.RecoveryAction);
                            _telemetry.RecordActivity("Workflow", $"recovery_{recoveryStrategy.RecoveryAction}");
                        }
                        Console.WriteLine($"[{eventType}] {evt}");
                        break;

                    case RecoveryActionEvent:
                        activity?.SetTag("workflow.recovery_action_taken", true);
                        _telemetry.RecordActivity("Workflow", "recovery_action");
                        Console.WriteLine($"[{eventType}] {evt}");
                        break;

                    case Microsoft.Agents.AI.Workflows.WorkflowOutputEvent outputEvent:
                        activity?.SetTag("workflow.output_generated", true);
                        activity?.SetTag($"workflow.output_length", outputEvent.ToString()?.Length ?? 0);
                        _telemetry.RecordActivity("Workflow", "output_generated");
                        Console.WriteLine($"\n>>> OUTPUT: {outputEvent}\n");
                        break;
                        
                    default:
                Console.WriteLine($"[{eventType}] {evt}");
                        break;
                }
            }
            
            // Log workflow completion metrics
            activity?.SetTag("workflow.completion_time", DateTimeOffset.UtcNow);
            activity?.SetTag("workflow.events.total", eventCounts.Values.Sum());
            activity?.SetTag("workflow.events.plan_generated", eventCounts["PlanGeneratedEvent"]);
            activity?.SetTag("workflow.events.nodes_executed", eventCounts["NodeExecutedEvent"]);
            activity?.SetTag("workflow.events.verifications", eventCounts["VerificationCompletedEvent"]);
            activity?.SetTag("workflow.events.recoveries", eventCounts["RecoveryStrategyDeterminedEvent"]);
            activity?.SetTag("workflow.events.outputs", eventCounts["WorkflowOutputEvent"]);
            
            _logger.LogInformation("Workflow completed successfully. Events: {EventSummary}", 
                string.Join(", ", eventCounts.Select(kvp => $"{kvp.Key}: {kvp.Value}")));
                
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }
}
