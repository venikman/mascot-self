using Azure.AI.OpenAI;
using AgentLmLocal.Configuration;
using AgentLmLocal.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using WorkflowCustomAgentExecutorsSample;

namespace AgentLmLocal;

public static class Program
{
    private static Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var observabilitySection = builder.Configuration.GetSection("Observability");
        var serviceName = observabilitySection.GetValue<string>("ServiceName") ?? "AgentLmLocal";
        var serviceNamespace = observabilitySection.GetValue<string>("ServiceNamespace") ?? "multi-agent-workflow";
        var serviceVersion = observabilitySection.GetValue<string>("ServiceVersion") ?? "1.0.0";

        // Configure Serilog for structured logging to stdout
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.WithProperty("service.name", serviceName)
                .Enrich.WithProperty("service.version", serviceVersion)
                .Enrich.WithProperty("service.namespace", serviceNamespace);
        });

        // Configure OpenTelemetry for distributed tracing
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName,
                    ["service.namespace"] = serviceNamespace
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddSource("AgentLmLocal")
                .AddSource("Microsoft.Agents.AI.*"));

        var config = AgentConfiguration.FromEnvironment();
        builder.Services.AddSingleton(config);

        builder.Services.AddSingleton<IChatClient>(_ => CreateChatClient(config));

        builder.Services.AddSingleton<AgentFactory>();
        builder.Services.AddSingleton<LlmService>();
        builder.Services.AddSingleton<RunTracker>();
        builder.Services.AddSingleton<AgenticWorkflowExample>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/run", (AgenticWorkflowExample example, RunTracker tracker, ILoggerFactory loggerFactory, RunRequest? request) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Task))
            {
                return Results.BadRequest(new { error = "Task description is required." });
            }

            var workflowId = Guid.NewGuid().ToString("N");
            var logger = loggerFactory.CreateLogger("WorkflowRunner");

            tracker.Register(workflowId, request.Task);
            logger.LogInformation("Workflow {WorkflowId} started for task \"{Task}\"", workflowId, request.Task);

            _ = Task.Run(() => example.RunWorkflowAsync(workflowId, request.Task));

            return Results.Accepted($"/runs/{workflowId}", new { workflowId, status = "started" });
        });

        app.MapGet("/runs/{workflowId}", (RunTracker tracker, string workflowId) =>
        {
            return tracker.TryGetStatus(workflowId, out var status)
                ? Results.Ok(status)
                : Results.NotFound(new { workflowId, status = "not-found" });
        });

        return app.RunAsync();
    }

    private static IChatClient CreateChatClient(AgentConfiguration config) =>
        config.Provider switch
        {
            LlmProvider.AzureOpenAI => CreateAzureOpenAIChatClient(config),
            _ => CreateLmStudioChatClient(config)
        };

    private static IChatClient CreateLmStudioChatClient(AgentConfiguration config)
    {
        var lmStudioClient = new OpenAI.OpenAIClient(
            new ApiKeyCredential(config.ApiKey),
            new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(config.LmStudioEndpoint)
            });

        return lmStudioClient.GetChatClient(config.ModelId).AsIChatClient();
    }

    private static IChatClient CreateAzureOpenAIChatClient(AgentConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.AzureOpenAIEndpoint) ||
            string.IsNullOrWhiteSpace(config.AzureOpenAIApiKey) ||
            string.IsNullOrWhiteSpace(config.AzureOpenAIDeployment))
        {
            throw new InvalidOperationException(
                "Azure OpenAI provider selected, but AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY, and AZURE_OPENAI_DEPLOYMENT are not fully configured.");
        }

        var azureClient = new AzureOpenAIClient(
            new Uri(config.AzureOpenAIEndpoint),
            new ApiKeyCredential(config.AzureOpenAIApiKey),
            CreateAzureOpenAIOptions(config.AzureOpenAIApiVersion));

        return azureClient.GetChatClient(config.AzureOpenAIDeployment).AsIChatClient();
    }

    private static AzureOpenAIClientOptions CreateAzureOpenAIOptions(string apiVersion)
    {
        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            return new AzureOpenAIClientOptions();
        }

        if (TryParseServiceVersion(apiVersion, out var version))
        {
            return new AzureOpenAIClientOptions(version);
        }

        return new AzureOpenAIClientOptions();
    }

    private static bool TryParseServiceVersion(
        string apiVersion,
        out AzureOpenAIClientOptions.ServiceVersion version)
    {
        if (Enum.TryParse(apiVersion, ignoreCase: true, out version))
        {
            return true;
        }

        var normalized = "V" + apiVersion
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal)
            .Replace("preview", "Preview", StringComparison.OrdinalIgnoreCase);

        return Enum.TryParse(normalized, ignoreCase: false, out version);
    }

    private sealed record RunRequest(string Task);
}
