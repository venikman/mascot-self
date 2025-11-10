using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace AgentLmLocal;

/// <summary>
/// Emits a small burst of telemetry on application startup so dashboards have immediate data.
/// </summary>
public sealed class StartupTelemetryPing : IHostedService
{
    private readonly ILogger<StartupTelemetryPing> _logger;
    private readonly AgentInstrumentation _telemetry;

    public StartupTelemetryPing(ILogger<StartupTelemetryPing> logger, AgentInstrumentation telemetry)
    {
        _logger = logger;
        _telemetry = telemetry;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = AgentInstrumentation.ActivitySource.StartActivity("startup.telemetry.ping");
        activity?.AddBaggage("startup", "true");
        activity?.SetTag("ping.kind", "startup");

        _telemetry.RecordActivity("Startup", "ping", duration: 1);

        _logger.LogInformation("Startup telemetry ping emitted: traces, metrics, and log");
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
