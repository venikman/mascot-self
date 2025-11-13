namespace AgentHello.Telemetry;

public class TelemetryOptions
{
    public const string SectionName = "Telemetry";
    public const string LogsOnlyMode = "LogsOnly";

    public string Mode { get; set; } = LogsOnlyMode;

    public bool IsLogsOnly() => string.Equals(Mode, LogsOnlyMode, StringComparison.OrdinalIgnoreCase);
}