using System.ClientModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using WorkflowCustomAgentExecutorsSample.Agents;

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
public static class AgenticWorkflowExample
{
    public static async Task RunExample()
    {
        Console.WriteLine("=== Agentic Workflow Example ===\n");
        Console.WriteLine("This example demonstrates multi-agent coordination with:");
        Console.WriteLine("- Planner: Task decomposition into DAGs");
        Console.WriteLine("- Executor: Step-by-step execution");
        Console.WriteLine("- Verifier: Quality evaluation");
        Console.WriteLine("- Recovery: Error handling");
        Console.WriteLine("- Retriever: Knowledge augmentation\n");

        // Set up the LLM client (using LM Studio as configured in original Program.cs)
        var endpoint = Environment.GetEnvironmentVariable("LMSTUDIO_ENDPOINT") ?? "http://localhost:1234/v1";
        var apiKey = Environment.GetEnvironmentVariable("LMSTUDIO_API_KEY") ?? "lm-studio";
        var modelId = Environment.GetEnvironmentVariable("LMSTUDIO_MODEL") ?? "openai/gpt-oss-20b";

        OpenAIClient lmStudioClient = new(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            });

        var chatClient = lmStudioClient.GetChatClient(modelId).AsIChatClient();

        // Create all the agents
        var planner = new PlannerAgent("Planner", chatClient);
        var executor = new ExecutorAgent("Executor", chatClient)
        {
            VerificationInterval = 2 // Verify every 2 nodes
        };
        var verifier = new VerifierAgent("Verifier", chatClient)
        {
            MinimumQualityScore = 7,
            EnableSpeculativeExecution = true
        };
        var recoveryHandler = new RecoveryHandlerAgent("RecoveryHandler", chatClient)
        {
            MaxRetries = 3,
            EnableStateSnapshots = true
        };
        var retriever = new RetrieverAgent("Retriever", chatClient)
        {
            EnableCaching = true,
            MinimumRelevanceScore = 0.7
        };

        // Build the workflow with agent coordination
        var workflow = new WorkflowBuilder(planner)
            // Planner → Executor
            .AddEdge(planner, executor)
            // Executor → Verifier
            .AddEdge(executor, verifier)
            // Verifier → RecoveryHandler (on failure)
            .AddEdge(verifier, recoveryHandler)
            // RecoveryHandler → Executor (retry)
            .AddEdge(recoveryHandler, executor)
            // Executor can also call Retriever when knowledge is needed
            .AddEdge(executor, retriever)
            .AddEdge(retriever, executor)
            // Set output from verifier (success path)
            .WithOutputFrom(verifier)
            .Build();

        // Example 1: Complex task that requires planning and execution
        Console.WriteLine("\n--- Example 1: Complex Multi-Step Task ---");
        await RunWorkflowExample(
            workflow,
            "Create a comprehensive marketing campaign for a new AI-powered productivity app, " +
            "including market research, target audience analysis, content strategy, and launch timeline.");

        Console.WriteLine("\n\n--- Example 2: Task with Knowledge Retrieval ---");
        await RunWorkflowExample(
            workflow,
            "Research and summarize the latest trends in quantum computing applications for 2024, " +
            "then create a technical presentation outline.");

        Console.WriteLine("\n\n=== Agentic Workflow Example Complete ===");
    }

    private static async Task RunWorkflowExample(Workflow workflow, string task)
    {
        Console.WriteLine($"\nTask: {task}\n");
        Console.WriteLine("Executing workflow...\n");

        try
        {
            await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, input: task);
            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                // Display events from all agents
                switch (evt)
                {
                    case PlanGeneratedEvent:
                    case NodeExecutedEvent:
                    case VerificationCompletedEvent:
                    case RecoveryStrategyDeterminedEvent:
                    case RecoveryActionEvent:
                    case RetrievalEvent:
                        Console.WriteLine($"[{evt.GetType().Name}] {evt}");
                        break;

                    case WorkflowOutputEvent outputEvent:
                        Console.WriteLine($"\n>>> OUTPUT: {outputEvent}\n");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nWorkflow Error: {ex.Message}");
        }
    }

    // Visualization functionality has been removed
    // See git history if you need to restore it
}
