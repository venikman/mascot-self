# Serilog + OpenTelemetry Hybrid Logging

Quick reference for the hybrid observability approach used in this prototype.

## Overview

This project uses a **hybrid observability approach**:
- **Serilog** for structured logging (JSON Lines to stdout)
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
```

## Output Format

**Example log entry** (Compact JSON / JSONL):

```json
{"@t":"2025-11-11T14:30:45.1234567Z","@mt":"Workflow {WorkflowId} executing task \"{Task}\"","@l":"Information","WorkflowId":"abc123def456","Task":"Plan a local AI meetup","SourceContext":"AgenticWorkflowExample","ServiceName":"AgentLmLocal","SpanId":"00f067aa0ba902b7","TraceId":"4bf92f3577b34da6a3ce929d0e0e4736"}
```

**Key fields:**
- `@t` - Timestamp (ISO 8601 UTC)
- `@mt` - Message template
- `@l` - Log level (Information, Warning, Error)
- `@x` - Exception details (if present)
- `WorkflowId`, `Task` - Structured properties from your code
- **`SpanId`** - Current span ID from OpenTelemetry
- **`TraceId`** - Trace ID from OpenTelemetry (links logs across distributed services)

## Configuration

### Program.cs

```csharp
// Configure Serilog for structured logging
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.WithSpan()              // ⭐ Adds TraceId/SpanId from OTEL
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("ServiceName", "AgentLmLocal"));

// Configure OpenTelemetry for distributed tracing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("AgentLmLocal", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("AgentLmLocal"));
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

## Usage

**Basic structured logging:**
```csharp
_logger.LogInformation("Workflow {WorkflowId} executing task \"{Task}\"",
    workflowId, task);

// Output includes: WorkflowId, Task, TraceId, SpanId (automatically!)
```

**With additional context:**
```csharp
using Serilog.Context;

using (LogContext.PushProperty("UserId", userId))
{
    _logger.LogInformation("User action performed");
    // All logs in this scope include UserId
}
```

**Custom spans for manual tracing:**
```csharp
using System.Diagnostics;

private static readonly ActivitySource ActivitySource = new("AgentLmLocal");

public async Task ProcessAsync(string workflowId)
{
    using var activity = ActivitySource.StartActivity("ProcessWorkflow");
    activity?.SetTag("workflow.id", workflowId);

    _logger.LogInformation("Processing {WorkflowId}", workflowId);
    // Log automatically includes TraceId/SpanId from activity

    await DoWorkAsync();
}
```

## Key Benefits

1. **Zero custom code** - Standard Serilog + OTEL packages
2. **Automatic correlation** - TraceId/SpanId automatically added to all logs
3. **Single-line JSON** - Each log is one line, perfect for log aggregation tools
4. **Structured data** - All properties are queryable fields, not text parsing
5. **Distributed tracing** - Correlate logs across multiple services using TraceId

## Required NuGet Packages

```xml
<!-- Serilog -->
<PackageReference Include="Serilog.AspNetCore" />
<PackageReference Include="Serilog.Formatting.Compact" />
<PackageReference Include="Serilog.Enrichers.Span" />
<PackageReference Include="Serilog.Enrichers.Environment" />

<!-- OpenTelemetry -->
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
```

## Troubleshooting

**TraceId/SpanId not appearing:**
- Ensure `AddAspNetCoreInstrumentation()` is configured
- Verify `Serilog.Enrichers.Span` package is installed
- Check that enricher is added: `.Enrich.WithSpan()`

**Logs not in JSON format:**
- Must specify formatter: `.WriteTo.Console(new CompactJsonFormatter())`

**Multiple lines per log entry:**
- Use `CompactJsonFormatter` (not `RenderedCompactJsonFormatter`)
