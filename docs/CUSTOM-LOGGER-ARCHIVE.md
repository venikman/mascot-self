# Custom JSON Lines Logger - Archive

**Status:** DEPRECATED - Replaced by Serilog + OpenTelemetry hybrid on 2025-11-11

## Historical Context

This document archives the custom `JsonLines.Logging` exporter that was previously used in this project. It has been replaced by a Serilog + OpenTelemetry hybrid approach for better maintainability and ecosystem support.

## What It Was

A custom OpenTelemetry `LogRecord` exporter that formatted logs as single-line JSON (JSON Lines format) for stdout → Splunk ingestion.

### Implementation Details

**Project:** `JsonLines.Logging/`

**Key Components:**
1. `JsonLinesLogRecordExporter.cs` (~135 lines)
   - Extended `BaseExporter<LogRecord>`
   - Serialized `LogRecord` → JSON → single line
   - Thread-safe with lock synchronization

2. `JsonLinesLogRecordExportProcessor.cs`
   - Bridged exporter to OTEL pipeline
   - Handled force flush and disposal

3. `JsonLinesExporterOptions.cs`
   - Configuration (TextWriter, JsonSerializerOptions)

4. `JsonLinesLoggerOptionsExtensions.cs`
   - `AddJsonLinesExporter()` extension method

### Output Format

```json
{
  "timestamp": "2025-11-11T14:30:45.1234567Z",
  "severity": "Information",
  "category": "AgenticWorkflowExample",
  "eventId": 0,
  "message": "Workflow abc123 executing task \"create report\"",
  "exception": null,
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId": "00f067aa0ba902b7",
  "attributes": {
    "WorkflowId": "abc123",
    "Task": "create report"
  }
}
```

### Field Mapping

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| `timestamp` | DateTimeOffset | `LogRecord.Timestamp` | ISO 8601 UTC |
| `severity` | string | `LogRecord.LogLevel.ToString()` | Log level |
| `category` | string | `LogRecord.CategoryName` | Logger category |
| `eventId` | int | `LogRecord.EventId.Id` | Event identifier |
| `message` | string | `LogRecord.FormattedMessage` | Human-readable message |
| `exception` | string? | `LogRecord.Exception?.ToString()` | Stack trace |
| `traceId` | string? | `LogRecord.TraceId.ToHexString()` | Distributed trace ID |
| `spanId` | string? | `LogRecord.SpanId.ToHexString()` | Current span ID |
| `attributes` | object | `LogRecord.Attributes` | Structured properties |

## Why It Was Replaced

### Reasons for Migration

1. **Maintenance Burden**
   - Custom code requires ongoing maintenance
   - Need to handle .NET version updates
   - Responsible for bug fixes and testing

2. **Limited Flexibility**
   - Single output destination (stdout only)
   - Adding new outputs required code changes
   - Configuration changes required recompilation

3. **Ecosystem Gap**
   - No community support
   - Limited to features we implemented
   - Missing advanced capabilities (sampling, filtering, enrichment)

4. **Testing Overhead**
   - Required custom unit tests
   - Edge cases to maintain
   - Integration testing burden

### What Serilog Provides

✅ **Zero custom code** - Community-maintained
✅ **Multiple sinks** - 100+ output destinations
✅ **Configuration-driven** - No recompilation needed
✅ **Rich enrichment** - 50+ enrichers available
✅ **Battle-tested** - Millions of production hours
✅ **Same output** - Still JSON Lines to stdout

## Migration Impact

### Code Changes

**Before:**
```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
    logging.AddJsonLinesExporter();  // Custom
});
```

**After:**
```csharp
builder.Host.UseSerilog((context, services, configuration) => configuration
    .Enrich.WithSpan()  // TraceId/SpanId from OTEL
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation());
```

### Application Code

**No changes required!** All `ILogger<T>` usage remained identical.

### Output Format Differences

| Aspect | Custom Logger | Serilog Compact JSON |
|--------|---------------|----------------------|
| **Field naming** | `timestamp`, `severity`, `message` | `@t`, `@l`, `@mt` |
| **Format** | Custom | Industry standard (CLEF) |
| **Size** | Baseline | ~30% smaller |
| **Splunk parsing** | Custom props.conf | Standard CLEF parser |

### Splunk Query Changes

**Before:**
```spl
index=app severity=Error
index=app attributes.WorkflowId="abc123"
```

**After:**
```spl
index=app @l=Error
index=app WorkflowId="abc123"  # Attributes flattened
```

## Custom Implementation Highlights

### Thread Safety

```csharp
// Lock-based synchronization for concurrent writes
lock (_syncRoot)
{
    _writer.WriteLine(line);
}
```

### Null Handling

```csharp
// Omit null fields from JSON
new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

### Timestamp Normalization

```csharp
// Always UTC, handle different DateTimeKind
if (record.Timestamp.Kind == DateTimeKind.Utc)
    return new DateTimeOffset(record.Timestamp);

return new DateTimeOffset(DateTime.SpecifyKind(record.Timestamp, DateTimeKind.Utc));
```

### TraceId/SpanId Conversion

```csharp
// Convert ActivityTraceId → hex string
private static string? ConvertTraceId(ActivityTraceId traceId) =>
    traceId == default ? null : traceId.ToHexString();
```

## Lessons Learned

### What Worked Well

✅ **OpenTelemetry integration** - Clean abstraction over `LogRecord`
✅ **JSON Lines format** - Perfect for log aggregation
✅ **Thread safety** - Lock-based approach was reliable
✅ **Splunk compatibility** - Single-line JSON worked perfectly

### What Could Have Been Better

⚠️ **Reinventing the wheel** - Serilog already solves this
⚠️ **Limited flexibility** - Hard to add features (sampling, filtering)
⚠️ **Configuration rigidity** - Changes required code modifications
⚠️ **Testing burden** - Had to test edge cases ourselves

## If You Need to Restore It

If you ever need to reference the old implementation:

1. Check git history: `git log --all --oneline -- JsonLines.Logging/`
2. Restore from git: `git checkout <commit-hash> -- JsonLines.Logging/`

**Last commit before deletion:** Check git log for 2025-11-11

## Recommended Reading

- [Why custom exporters are discouraged](https://opentelemetry.io/docs/specs/otel/logs/sdk/#built-in-processors)
- [Serilog vs Custom Logging](https://nblumhardt.com/2016/07/serilog-2-write-to-logger/)
- [Compact Log Event Format (CLEF)](https://github.com/serilog/serilog-formatting-compact)

---

**Migration completed:** 2025-11-11
**Deleted projects:** `JsonLines.Logging/`, `JsonLines.Logging.Tests/`
**Replaced with:** Serilog + OpenTelemetry hybrid
**Migration guide:** See `SERILOG-MIGRATION-SUMMARY.md`
