# Serilog + OpenTelemetry Hybrid Logging Setup

## Overview

This project uses a **hybrid observability approach**:
- **Serilog** for structured logging (JSON Lines to stdout → Splunk)
- **OpenTelemetry** for distributed tracing
- **Automatic correlation** via `Serilog.Enrichers.Span` (TraceId/SpanId in logs)

## Architecture

```
┌─────────────────────────────────────────────────────┐
│ Application Code                                    │
│   ILogger<T>.LogInformation(template, params)      │
└──────────────────┬──────────────────────────────────┘
                   │
          ┌────────┴────────┐
          ▼                 ▼
┌──────────────────┐  ┌─────────────────────┐
│ Serilog          │  │ OpenTelemetry       │
│ - Logging        │  │ - Tracing           │
│ - Enrichment     │◄─┤ - Activity/Span     │
│ - Formatting     │  │ - Instrumentation   │
└────────┬─────────┘  └─────────────────────┘
         │
         ▼
   CompactJsonFormatter
         │
         ▼
      stdout (JSON Lines)
         │
         ▼
   Splunk Forwarder
```

## Output Format

### Example Log Entry (Compact JSON)

```json
{"@t":"2025-11-11T14:30:45.1234567Z","@mt":"Workflow {WorkflowId} executing task \"{Task}\"","@l":"Information","WorkflowId":"abc123def456","Task":"Plan a local AI meetup","SourceContext":"AgenticWorkflowExample","ServiceName":"AgentLmLocal","MachineName":"worker-node-01","EnvironmentName":"Production","SpanId":"00f067aa0ba902b7","TraceId":"4bf92f3577b34da6a3ce929d0e0e4736"}
```

### Field Breakdown

| Field | Description | Source |
|-------|-------------|--------|
| `@t` | Timestamp (ISO 8601 UTC) | Serilog |
| `@mt` | Message template | Serilog |
| `@l` | Log level | Serilog |
| `@x` | Exception details (if present) | Serilog |
| `WorkflowId`, `Task` | Structured properties | Your code |
| `SourceContext` | Logger category name | Serilog |
| `ServiceName` | Service identifier | Enricher (config) |
| `MachineName` | Hostname | `Serilog.Enrichers.Environment` |
| `EnvironmentName` | Environment (Dev/Prod) | `Serilog.Enrichers.Environment` |
| **`SpanId`** | Current span identifier | **`Serilog.Enrichers.Span` + OTEL** |
| **`TraceId`** | Trace identifier | **`Serilog.Enrichers.Span` + OTEL** |

## Configuration

### NuGet Packages

```xml
<!-- Serilog -->
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
<PackageReference Include="Serilog.Enrichers.Span" Version="3.1.0" />
<PackageReference Include="Serilog.Enrichers.Environment" Version="3.0.1" />

<!-- OpenTelemetry -->
<PackageReference Include="OpenTelemetry" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.9.0" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
```

### Program.cs Configuration

```csharp
// Configure Serilog for structured logging
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()              // ⭐ Adds TraceId/SpanId from OTEL
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("ServiceName", "AgentLmLocal")
    .WriteTo.Console(new CompactJsonFormatter()));

// Configure OpenTelemetry for distributed tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("AgentLmLocal", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()   // Auto-instrument HTTP requests
        .AddHttpClientInstrumentation()   // Auto-instrument HTTP clients
        .AddSource("AgentLmLocal"));      // Custom activity source
```

### appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "AgentLmLocal": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithSpan", "WithMachineName", "WithEnvironmentName"]
  }
}
```

## Usage Examples

### Basic Structured Logging

```csharp
_logger.LogInformation("Workflow {WorkflowId} executing task \"{Task}\"",
    workflowId, task);

// Output includes: WorkflowId, Task, TraceId, SpanId
```

### Logging with LogContext (Request-Scoped Properties)

```csharp
using Serilog.Context;

using (LogContext.PushProperty("UserId", userId))
using (LogContext.PushProperty("TenantId", tenantId))
{
    _logger.LogInformation("User action performed");
    // All logs in this scope include UserId and TenantId
}
```

### Custom Activity Source for Manual Spans

```csharp
using System.Diagnostics;

public class MyService
{
    private static readonly ActivitySource ActivitySource = new("AgentLmLocal");
    private readonly ILogger<MyService> _logger;

    public async Task ProcessAsync(string workflowId)
    {
        using var activity = ActivitySource.StartActivity("ProcessWorkflow");
        activity?.SetTag("workflow.id", workflowId);

        _logger.LogInformation("Processing {WorkflowId}", workflowId);
        // Log automatically includes TraceId/SpanId from activity

        await DoWorkAsync();
    }
}
```

## Splunk Integration

### Splunk Configuration (props.conf)

```ini
[your_sourcetype]
INDEXED_EXTRACTIONS = json
KV_MODE = json
TIMESTAMP_FIELDS = @t
TIME_FORMAT = %Y-%m-%dT%H:%M:%S.%N%z
MAX_TIMESTAMP_LOOKAHEAD = 32
SHOULD_LINEMERGE = false
LINE_BREAKER = ([\r\n]+)
```

### Splunk Search Examples

```spl
# Find all logs for a specific trace
index=app TraceId="4bf92f3577b34da6a3ce929d0e0e4736"
| table @t, @l, @mt, SourceContext, SpanId

# Find errors in a workflow
index=app @l=Error WorkflowId="abc123"
| table @t, @mt, @x

# Track request flow through services
index=app TraceId="*"
| stats count by TraceId, SpanId, ServiceName
| sort @t

# Alert on exception spikes
index=app @x!=null
| timechart span=5m count
| where count > 100
```

## Key Benefits

### 1. **Zero Custom Code**
- No custom exporter to maintain
- Standard Serilog + OTEL packages
- Configuration-driven

### 2. **Automatic Correlation**
- TraceId/SpanId automatically added to logs
- Correlate logs ↔ traces without manual instrumentation
- Works across service boundaries (distributed tracing)

### 3. **Flexible Output**
- Add multiple sinks easily (File, Seq, Splunk HEC)
- Change formatting without code changes
- Filter/sample at configuration level

### 4. **Rich Ecosystem**
- 100+ Serilog sinks available
- OTEL instrumentation for all major libraries
- Community support for both

### 5. **Splunk-Optimized**
- Single-line JSON (line-based ingestion)
- Compact format (reduces log volume ~30%)
- Auto-parsed fields for querying

## Comparison: Custom Exporter vs. Serilog

| Aspect | Custom JsonLines Exporter | Serilog Hybrid |
|--------|---------------------------|----------------|
| **Custom code** | ~135 lines | 0 lines |
| **Maintenance** | You own it | Community-maintained |
| **Flexibility** | Single output (stdout) | Multiple sinks |
| **Configuration** | Code changes | appsettings.json |
| **Ecosystem** | Limited | 100+ sinks, enrichers |
| **TraceId/SpanId** | Built-in (OTEL) | Via enricher |
| **Performance** | Fast | Very fast |
| **Testing** | Your tests | Battle-tested |

## Migration from Custom Exporter

If you're migrating from `JsonLines.Logging`, the changes are minimal:

**Before (Custom Exporter):**
```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
    logging.AddJsonLinesExporter();  // Custom
});
```

**After (Serilog Hybrid):**
```csharp
builder.Host.UseSerilog((context, services, configuration) => configuration
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation());
```

**Application code remains unchanged!** Both use `ILogger<T>` interface.

## Troubleshooting

### TraceId/SpanId not appearing in logs

**Cause:** OpenTelemetry Activity not created for the request.

**Solution:**
1. Ensure `AddAspNetCoreInstrumentation()` is configured
2. Check that the request path is not filtered
3. Verify `Serilog.Enrichers.Span` package is installed

### Logs not in JSON format

**Cause:** Console formatter not configured correctly.

**Solution:**
```csharp
.WriteTo.Console(new CompactJsonFormatter())  // Must specify formatter
```

### Multiple lines per log entry

**Cause:** Using `RenderedCompactJsonFormatter` instead of `CompactJsonFormatter`.

**Solution:**
```csharp
// ✅ Correct (single-line)
new CompactJsonFormatter()

// ❌ Wrong (multi-line)
new RenderedCompactJsonFormatter()
```

## Performance Considerations

### Log Volume Reduction

Compact JSON format reduces log size by ~30% compared to human-readable text:
- No indentation/whitespace
- Null fields omitted
- Abbreviated field names (`@t`, `@mt`, `@l`)

### Buffering

Serilog batches writes by default. For high-throughput scenarios:

```csharp
.WriteTo.Console(
    new CompactJsonFormatter(),
    restrictedToMinimumLevel: LogEventLevel.Information,
    bufferSize: 10000)
```

### Async Logging

For even better performance, use async sinks:

```csharp
.WriteTo.Async(a => a.Console(new CompactJsonFormatter()))
```

## References

- [Serilog Documentation](https://serilog.net/)
- [Serilog.Enrichers.Span GitHub](https://github.com/RehanSaeed/Serilog.Enrichers.Span)
- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/dotnet/)
- [Compact JSON Format Specification](https://github.com/serilog/serilog-formatting-compact)
- [Splunk JSON Logging Best Practices](https://docs.splunk.com/Documentation/Splunk/latest/Data/Indexjson)
