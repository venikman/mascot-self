// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using WorkflowCustomAgentExecutorsSample;
using AgentLmLocal.Configuration;
using AgentLmLocal.Services;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System;

namespace AgentLmLocal;

/// <summary>
/// Entry point for the agentic workflow system.
/// Runs a multi-agent workflow demonstrating coordinated planning, execution,
/// verification, recovery, and knowledge retrieval.
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
/// - OTLP_ENDPOINT: The OTLP endpoint for telemetry export (default: http://localhost:4317)
/// </remarks>
public static class Program
{
    private static Task Main(string[] args)
    {
        // Enable explicit context propagation (TraceContext + Baggage)
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
        {
            new TraceContextPropagator(),
            new BaggagePropagator()
        }));

        var builder = WebApplication.CreateBuilder(args);

        // Load configuration from environment
        var config = AgentConfiguration.FromEnvironment();
        builder.Services.AddSingleton(config);

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;

            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(config.OtlpEndpoint);
                otlpOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        });

        // Configure OpenTelemetry
        builder.Services.AddAgentTelemetry();

        // Register core services
        builder.Services.AddSingleton<AgentInstrumentation>();
        builder.Services.AddHostedService<StartupTelemetryPing>();

        // Register LLM client
        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var lmStudioClient = new OpenAI.OpenAIClient(
                new ApiKeyCredential(config.ApiKey),
                new OpenAI.OpenAIClientOptions
                {
                    Endpoint = new Uri(config.LmStudioEndpoint)
                });

            return lmStudioClient.GetChatClient(config.ModelId).AsIChatClient();
        });

        // Register agent services
        builder.Services.AddSingleton<AgentFactory>();
        builder.Services.AddSingleton<LlmService>();
        builder.Services.AddSingleton<AgenticWorkflowExample>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/run", async (AgenticWorkflowExample example, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("AgentRunEndpoint");
            var workflowId = Guid.NewGuid().ToString("N");
            logger.LogInformation("Received /run request. Starting workflow: {WorkflowId}", workflowId);

            _ = Task.Run(() => example.RunExample());

            return Results.Accepted($"/runs/{workflowId}", new { workflowId, status = "started" });
        });

        return app.RunAsync();
    }
}
