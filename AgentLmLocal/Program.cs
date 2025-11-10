// Copyright (c) Microsoft. All rights reserved.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WorkflowCustomAgentExecutorsSample;
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

        // Logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;

            var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                           ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")
                           ?? "http://localhost:19149";

            options.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(endpoint);
                otlpOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        });

        // Services
        builder.Services.AddSingleton<AgentInstrumentation>();
        builder.Services.AddSingleton<AgenticWorkflowExample>();
        builder.Services.AddHostedService<StartupTelemetryPing>();

        // OpenTelemetry
        var serviceName = "agentlm-local";
        var serviceVersion = "1.0.0";
        var environment = Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT") ?? "development";
        var instanceId = Environment.MachineName;
        var samplingRatioEnv = Environment.GetEnvironmentVariable("OTEL_TRACE_SAMPLING_RATIO");
        var samplingRatio = 1.0;
        if (double.TryParse(samplingRatioEnv, out var parsedRatio))
        {
            samplingRatio = Math.Clamp(parsedRatio, 0.0, 1.0);
        }

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new []
                {
                    new KeyValuePair<string, object>("deployment.environment", environment),
                    new KeyValuePair<string, object>("service.instance.id", instanceId)
                }))
            .WithTracing(tracing =>
            {
                tracing.AddSource("AgenticWorkflow")
                       .AddHttpClientInstrumentation()
                       .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                       .AddOtlpExporter(otlpOptions =>
                       {
                           var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                          ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")
                                          ?? "http://localhost:19149";
                           otlpOptions.Endpoint = new Uri(endpoint);

                           var protocolEnv = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
                           if (!string.IsNullOrWhiteSpace(protocolEnv))
                           {
                               otlpOptions.Protocol = protocolEnv.Equals("grpc", StringComparison.OrdinalIgnoreCase)
                                   ? OtlpExportProtocol.Grpc
                                   : OtlpExportProtocol.HttpProtobuf;
                           }
                           else
                           {
                               var useGrpc = endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase);
                               otlpOptions.Protocol = useGrpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
                           }
                       });
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter("AgenticWorkflow")
                       .AddRuntimeInstrumentation()
                       .AddView(
                           instrumentName: "agent.execution.duration",
                           new ExplicitBucketHistogramConfiguration
                           {
                               Boundaries = new double[] { 5, 10, 20, 50, 100, 250, 500, 1000, 2000 }
                           })
                       .AddOtlpExporter(otlpOptions =>
                       {
                           var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                                          ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")
                                          ?? "http://localhost:19149";
                           otlpOptions.Endpoint = new Uri(endpoint);
                           otlpOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                       });
            });

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
