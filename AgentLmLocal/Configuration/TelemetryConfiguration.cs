using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgentLmLocal.Configuration;

/// <summary>
/// Extension methods for configuring OpenTelemetry for the agent system.
/// </summary>
public static class TelemetryConfiguration
{
    /// <summary>
    /// Adds comprehensive OpenTelemetry telemetry to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">The service name for telemetry.</param>
    /// <param name="serviceVersion">The service version.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentTelemetry(
        this IServiceCollection services,
        string serviceName = "agentlm-local",
        string serviceVersion = "1.0.0")
    {
        var environment = Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT") ?? "development";
        var instanceId = Environment.MachineName;
        var samplingRatioEnv = Environment.GetEnvironmentVariable("OTEL_TRACE_SAMPLING_RATIO");
        var samplingRatio = 1.0;
        if (double.TryParse(samplingRatioEnv, out var parsedRatio))
        {
            samplingRatio = Math.Clamp(parsedRatio, 0.0, 1.0);
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new[]
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
                           var endpoint = GetOtlpEndpoint();
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
                           var endpoint = GetOtlpEndpoint();
                           otlpOptions.Endpoint = new Uri(endpoint);
                           otlpOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                       });
            });

        return services;
    }

    /// <summary>
    /// Gets the OTLP endpoint from environment variables with fallback to default.
    /// </summary>
    private static string GetOtlpEndpoint()
    {
        return Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
               ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")
               ?? "http://localhost:19149";
    }
}
