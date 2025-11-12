using AgentLmLocal.Configuration;
using AgentLmLocal.Services;
using AgentLmLocal.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Net.Http;
using System;
using System.Text.Json;
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

        builder.Services.AddHttpClient("openai", client =>
        {
            client.BaseAddress = new Uri(config.OpenAIBaseUrl);
            if (!string.IsNullOrWhiteSpace(config.OpenAIApiKey))
            {
                client.DefaultRequestHeaders.Add("api_key", config.OpenAIApiKey);
            }
            if (!string.IsNullOrWhiteSpace(config.OpenAIVersion))
            {
                client.DefaultRequestHeaders.Add("version", config.OpenAIVersion);
            }
        });

        builder.Services.AddSingleton<IChatClient>(sp => CreateOpenAIChatClient(config, sp.GetRequiredService<IHttpClientFactory>()));

        builder.Services.AddSingleton<AgentFactory>();
        builder.Services.AddSingleton<LlmService>();
        builder.Services.AddSingleton<RunTracker>();
        builder.Services.AddSingleton<AgenticWorkflowExample>();

        var app = builder.Build();

        // Enable static files for frontend
        // IMPORTANT: UseDefaultFiles MUST come before UseStaticFiles for SPA routing
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // OTEL proxy endpoint - receives telemetry from frontend
        app.MapPost("/otel/traces", (ILoggerFactory loggerFactory, OtelTraceRequest? traceRequest) =>
        {
            if (traceRequest is null)
            {
                return Results.BadRequest(new { error = "Invalid trace data" });
            }

            var logger = loggerFactory.CreateLogger("OtelProxy");

            // Log each span in JSONL format
            // Add null checks to prevent NullReferenceException on malformed OTLP payloads
            foreach (var resourceSpan in traceRequest.ResourceSpans ?? Enumerable.Empty<ResourceSpans>())
            {
                var resourceAttributes = resourceSpan.Resource?.Attributes
                    ?.ToDictionary(kv => kv.Key, kv => GetAttributeValue(kv.Value)) ?? new Dictionary<string, object?>();

                foreach (var scopeSpan in resourceSpan.ScopeSpans ?? Enumerable.Empty<ScopeSpans>())
                {
                    foreach (var span in scopeSpan.Spans ?? Enumerable.Empty<Span>())
                    {
                        var spanAttributes = span.Attributes
                            ?.ToDictionary(kv => kv.Key, kv => GetAttributeValue(kv.Value)) ?? new Dictionary<string, object?>();

                        // Safely parse timestamps to prevent crashes on malformed data
                        if (!long.TryParse(span.StartTimeUnixNano, out var startNano) ||
                            !long.TryParse(span.EndTimeUnixNano, out var endNano))
                        {
                            logger.LogWarning(
                                "Invalid timestamp format for span {SpanName} (TraceId: {TraceId}). Skipping duration calculation.",
                                span.Name,
                                span.TraceId);

                            // Log span without duration
                            logger.LogInformation(
                                "Frontend span: {SpanName} | TraceId: {TraceId} | SpanId: {SpanId} | Duration: N/A | Attributes: {Attributes} | Resource: {Resource}",
                                span.Name,
                                span.TraceId,
                                span.SpanId,
                                JsonSerializer.Serialize(spanAttributes),
                                JsonSerializer.Serialize(resourceAttributes));
                            continue;
                        }

                        var durationNano = endNano - startNano;
                        var durationMs = durationNano / 1_000_000.0;

                        logger.LogInformation(
                            "Frontend span: {SpanName} | TraceId: {TraceId} | SpanId: {SpanId} | Duration: {DurationMs}ms | Attributes: {Attributes} | Resource: {Resource}",
                            span.Name,
                            span.TraceId,
                            span.SpanId,
                            durationMs,
                            JsonSerializer.Serialize(spanAttributes),
                            JsonSerializer.Serialize(resourceAttributes));
                    }
                }
            }

            return Results.Ok(new { status = "ok" });
        });

        // Simple chat endpoint for AI chat
        app.MapPost("/chat", async (AgentFactory agentFactory, LlmService llmService, ILoggerFactory loggerFactory, ChatRequest? request) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new { error = "Message is required" });
            }

            var logger = loggerFactory.CreateLogger("ChatEndpoint");
            logger.LogInformation("Chat request received: {Message}", request.Message);

            try
            {
                var (agent, thread) = agentFactory.CreateAgent("You are a helpful assistant.");
                var responseText = await llmService.InvokeAsync(agent, thread, request.Message);

                logger.LogInformation("Chat response generated: {Response}", responseText ?? "No response");

                return Results.Ok(new { message = responseText ?? "No response" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error generating chat response");
                return Results.Problem("Error generating response");
            }
        });

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

    private static IChatClient CreateOpenAIChatClient(AgentConfiguration config, IHttpClientFactory httpClientFactory)
    {
        var httpClient = httpClientFactory.CreateClient("openai");

        var options = new OpenAI.OpenAIClientOptions
        {
            Endpoint = new Uri(config.OpenAIBaseUrl)
        };

        var client = new OpenAI.OpenAIClient(
            new ApiKeyCredential(config.OpenAIApiKey),
            options);

        return client.GetChatClient(config.OpenAIModelId).AsIChatClient();
    }

    private static object? GetAttributeValue(AttributeValue value)
    {
        if (value.StringValue is not null) return value.StringValue;
        if (value.IntValue.HasValue) return value.IntValue.Value;
        if (value.DoubleValue.HasValue) return value.DoubleValue.Value;
        if (value.BoolValue.HasValue) return value.BoolValue.Value;
        return null;
    }
}
