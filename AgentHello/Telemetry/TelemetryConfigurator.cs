using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

namespace AgentHello.Telemetry;

public static class TelemetryConfigurator
{
    public static TelemetryOptions Configure(WebApplicationBuilder builder)
    {
        var telemetrySection = builder.Configuration.GetSection(TelemetryOptions.SectionName);
        var telemetryOptions = telemetrySection.Get<TelemetryOptions>() ?? new TelemetryOptions();
        builder.Services.Configure<TelemetryOptions>(telemetrySection);
        var logsOnly = telemetryOptions.IsLogsOnly();

        if (!logsOnly)
        {
            builder.Logging.ClearProviders();
        }

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.WithProcessId()
            .Enrich.WithProcessName()
            .Enrich.WithMachineName()
            .Enrich.WithSpan()
            .Enrich.WithProperty("service.name", "AgentHello")
            .Enrich.WithProperty("deployment.environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateLogger();

        builder.Host.UseSerilog();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("AgentHello"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation();
                t.AddHttpClientInstrumentation();
                if (!logsOnly)
                {
                    t.AddOtlpExporter();
                }
            });

        return telemetryOptions;
    }
}