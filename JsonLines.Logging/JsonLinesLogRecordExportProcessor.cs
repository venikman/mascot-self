using System;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace JsonLines.Logging;

internal sealed class JsonLinesLogRecordExportProcessor : BaseProcessor<LogRecord>
{
    private readonly JsonLinesLogRecordExporter _exporter;

    public JsonLinesLogRecordExportProcessor(JsonLinesLogRecordExporter exporter)
    {
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
    }

    public override void OnEnd(LogRecord data)
    {
        _exporter.Export(new Batch<LogRecord>(new[] { data }, 1));
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        return _exporter.ForceFlush(timeoutMilliseconds);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _exporter.Dispose();
        }

        base.Dispose(disposing);
    }
}
