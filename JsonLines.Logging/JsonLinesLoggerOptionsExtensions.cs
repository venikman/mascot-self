using System;
using OpenTelemetry.Logs;

namespace JsonLines.Logging;

/// <summary>
/// Extension helpers for wiring the JsonLines exporter into OpenTelemetry logging.
/// </summary>
public static class JsonLinesLoggerOptionsExtensions
{
    public static OpenTelemetryLoggerOptions AddJsonLinesExporter(
        this OpenTelemetryLoggerOptions options,
        Action<JsonLinesExporterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var exporterOptions = new JsonLinesExporterOptions();
        configure?.Invoke(exporterOptions);

        var exporter = new JsonLinesLogRecordExporter(exporterOptions);

        return options.AddProcessor(new JsonLinesLogRecordExportProcessor(exporter));
    }
}
