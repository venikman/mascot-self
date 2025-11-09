// Copyright (c) Microsoft. All rights reserved.

using System.ClientModel;
using AgentLmLocal.Events;
using AgentLmLocal.Executors;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AgentLmLocal;

/// <summary>
/// This sample demonstrates how to create custom executors for AI agents.
/// This is useful when you want more control over the agent's behaviors in a workflow.
///
/// In this example, we create two custom executors:
/// 1. SloganWriterExecutor: An AI agent that generates slogans based on a given task.
/// 2. FeedbackExecutor: An AI agent that provides feedback on the generated slogans.
/// (These two executors manage the agent instances and their conversation threads.)
///
/// The workflow alternates between these two executors until the slogan meets a certain
/// quality threshold or a maximum number of attempts is reached.
/// </summary>
/// <remarks>
/// Pre-requisites:
/// - LM Studio (or compatible OpenAI API endpoint) running locally or remotely.
/// - A language model that supports structured JSON outputs.
///
/// Optional environment variables:
/// - LMSTUDIO_ENDPOINT: The API endpoint (default: http://localhost:1234/v1)
/// - LMSTUDIO_API_KEY: API key for authentication (default: lm-studio)
/// - LMSTUDIO_MODEL: The model ID to use (default: openai/gpt-oss-20b)
/// - MINIMUM_RATING: Minimum rating for slogan acceptance (default: 8)
/// - MAX_ATTEMPTS: Maximum refinement attempts (default: 3)
/// </remarks>
public static class Program
{
    // Default configuration values
    private const string DefaultEndpoint = "http://localhost:1234/v1";
    private const string DefaultApiKey = "lm-studio";
    private const string DefaultModelId = "openai/gpt-oss-20b";
    private const int DefaultMinimumRating = 8;
    private const int DefaultMaxAttempts = 3;

    private static async Task Main()
    {
        try
        {
            // Set up the LM Studio client (OpenAI-compatible API)
            var endpoint = Environment.GetEnvironmentVariable("LMSTUDIO_ENDPOINT") ?? DefaultEndpoint;
            var apiKey = Environment.GetEnvironmentVariable("LMSTUDIO_API_KEY") ?? DefaultApiKey;
            var modelId = Environment.GetEnvironmentVariable("LMSTUDIO_MODEL") ?? DefaultModelId;

            // Validate endpoint format
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
            {
                throw new ArgumentException($"Invalid endpoint URL: {endpoint}");
            }

            Console.WriteLine($"Connecting to LM Studio at: {endpoint}");
            Console.WriteLine($"Using model: {modelId}\n");

            OpenAIClient lmStudioClient = new(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions
                {
                    Endpoint = endpointUri
                });

            var chatClient = lmStudioClient.GetChatClient(modelId).AsIChatClient();

        // Parse configuration for feedback executor
        var minimumRating = int.TryParse(Environment.GetEnvironmentVariable("MINIMUM_RATING"), out var rating)
            ? rating : DefaultMinimumRating;
        var maxAttempts = int.TryParse(Environment.GetEnvironmentVariable("MAX_ATTEMPTS"), out var attempts)
            ? attempts : DefaultMaxAttempts;

        // Create the executors
        var sloganWriter = new SloganWriterExecutor("SloganWriter", chatClient);
        var feedbackProvider = new FeedbackExecutor("FeedbackProvider", chatClient)
        {
            MinimumRating = minimumRating,
            MaxAttempts = maxAttempts
        };

        // Build the workflow by adding executors and connecting them
        var workflow = new WorkflowBuilder(sloganWriter)
            .AddEdge(sloganWriter, feedbackProvider)
            .AddEdge(feedbackProvider, sloganWriter)
            .WithOutputFrom(feedbackProvider)
            .Build();

            // Execute the workflow
            await using StreamingRun run = await InProcessExecution.StreamAsync(workflow, input: "Create a slogan for a new electric SUV that is affordable and fun to drive.");
            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                if (evt is SloganGeneratedEvent or FeedbackEvent)
                {
                    // Custom events to allow us to monitor the progress of the workflow.
                    Console.WriteLine($"{evt}");
                }

                if (evt is WorkflowOutputEvent outputEvent)
                {
                    Console.WriteLine($"{outputEvent}");
                }
            }

            Console.WriteLine("\nWorkflow completed successfully!");
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Configuration error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Network error: Failed to connect to LM Studio endpoint.");
            Console.Error.WriteLine($"Details: {ex.Message}");
            Console.Error.WriteLine("\nPlease ensure LM Studio is running and accessible.");
            Environment.Exit(1);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Operation error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
