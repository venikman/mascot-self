using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace JsonLines.Logging;

/// <summary>
/// Exporter that formats each OpenTelemetry <see cref="LogRecord"/> as a single JSON line.
/// </summary>
public sealed class JsonLinesLogRecordExporter : BaseExporter<LogRecord>
{
    private readonly object _syncRoot = new();
    private readonly TextWriter _writer;
    private readonly bool _disposeWriter;
    private readonly JsonSerializerOptions _serializerOptions;

    public JsonLinesLogRecordExporter(JsonLinesExporterOptions? options = null)
    {
        options ??= new JsonLinesExporterOptions();

        _writer = options.Writer ?? Console.Out;
        _disposeWriter = options.DisposeWriter;
        _serializerOptions = options.SerializerOptions ?? CreateDefaultSerializerOptions();
    }

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            var payload = JsonLogPayload.From(record);
            var line = JsonSerializer.Serialize(payload, _serializerOptions);

            lock (_syncRoot)
            {
                _writer.WriteLine(line);
            }
        }

        return ExportResult.Success;
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        lock (_syncRoot)
        {
            _writer.Flush();
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _disposeWriter)
        {
            lock (_syncRoot)
            {
                _writer.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private static JsonSerializerOptions CreateDefaultSerializerOptions() => new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record JsonLogPayload
    {
        public DateTimeOffset Timestamp { get; init; }
        public string Severity { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public int EventId { get; init; }
        public string? Message { get; init; }
        public string? Exception { get; init; }
        public string? TraceId { get; init; }
        public string? SpanId { get; init; }
        public IReadOnlyDictionary<string, object?>? Attributes { get; init; }

        public static JsonLogPayload From(LogRecord record) => new()
        {
            Timestamp = GetTimestamp(record),
            Severity = record.LogLevel.ToString(),
            Category = record.CategoryName ?? string.Empty,
            EventId = record.EventId.Id,
            Message = record.FormattedMessage ?? record.Body?.ToString(),
            Exception = record.Exception?.ToString(),
            TraceId = ConvertTraceId(record.TraceId),
            SpanId = ConvertSpanId(record.SpanId),
            Attributes = ConvertAttributes(record.Attributes)
        };

        private static DateTimeOffset GetTimestamp(LogRecord record)
        {
            if (record.Timestamp == default)
            {
                return DateTimeOffset.UtcNow;
            }

            if (record.Timestamp.Kind == DateTimeKind.Utc)
            {
                return new DateTimeOffset(record.Timestamp);
            }

            return new DateTimeOffset(DateTime.SpecifyKind(record.Timestamp, DateTimeKind.Utc));
        }

        private static IReadOnlyDictionary<string, object?>? ConvertAttributes(IReadOnlyList<KeyValuePair<string, object?>>? attributes)
        {
            if (attributes is not { Count: > 0 })
            {
                return null;
            }

            var dictionary = new Dictionary<string, object?>(attributes.Count, StringComparer.Ordinal);
            foreach (var pair in attributes)
            {
                dictionary[pair.Key] = pair.Value;
            }

            return dictionary;
        }

        private static string? ConvertTraceId(ActivityTraceId traceId) => traceId == default ? null : traceId.ToHexString();

        private static string? ConvertSpanId(ActivitySpanId spanId) => spanId == default ? null : spanId.ToHexString();
    }
}
