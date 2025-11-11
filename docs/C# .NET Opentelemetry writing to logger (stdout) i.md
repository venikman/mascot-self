<img src="https://r2cdn.perplexity.ai/pplx-full-logo-primary-dark%402x.png" style="height:64px;margin-right:32px"/>

# C\# .NET Opentelemetry writing to logger (stdout) in .jsonl

Perfect! Now let me create a comprehensive summary document and generate the final answer:

# OpenTelemetry .NET Logging to stdout in JSONL Format

Based on extensive research into OpenTelemetry .NET implementations, I've identified **four viable approaches** for writing logs to stdout in JSONL (JSON Lines) format. Each approach has distinct tradeoffs in terms of complexity, OTLP compliance, and production readiness.

## Overview of Approaches

### Approach 1: Microsoft.Extensions.Logging with JSON Console Formatter

This approach uses .NET's **built-in JSON console formatter** combined with the OpenTelemetry logging provider. It's the simplest to implement but doesn't produce true OTLP-formatted output.[^1][^2]

**Key characteristics:**

- Native .NET 5+ support with no custom code required[^2]
- Outputs single-line JSON suitable for JSONL format
- Integrates seamlessly with ILogger interface
- Not OTLP protocol compliant (uses .NET's JSON format instead)

The configuration involves setting the console formatter to JSON and disabling indentation to ensure each log entry is a single line:[^2]

```csharp
builder.Services.Configure<JsonConsoleFormatterOptions>(options =>
{
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
});
```


### Approach 2: Custom OTLP File Exporter

This approach implements a **custom exporter** that follows the OpenTelemetry Protocol File Exporter specification. It provides full control over the output format and ensures OTLP compliance.[^3][^4]

**Key characteristics:**

- Full OTLP JSON format compliance per specification[^3]
- Outputs logs in the standardized `LogsData` structure
- Requires implementing BaseExporter<LogRecord>
- Supports batch processing configuration

The specification defines that logs should be written as JSON Lines format, where each line contains a complete OTLP `LogsData` object with `resourceLogs`, `scopeLogs`, and `logRecords` arrays.[^4][^3]

### Approach 3: Third-Party Package

The community package **Ozkanpakdil.OpenTelemetry.Exporter.Json.Console** provides a ready-made JSON console exporter. However, it's explicitly designed for debugging scenarios rather than production use.[^5]

**Key characteristics:**

- Minimal configuration required
- Quick setup for development environments
- Limited to debug/development scenarios per documentation[^5]
- May not be actively maintained


### Approach 4: OTLP Exporter with Collector (Recommended for Production)

This is the **most production-ready approach**, using the standard OTLP exporter to send logs to an OpenTelemetry Collector, which then outputs JSONL to stdout.[^6][^7]

**Key characteristics:**

- Decouples application from observability backend
- Leverages the collector's rich processor ecosystem[^8][^9]
- Supports batching, filtering, and enrichment at the collector level[^10][^11]
- Industry best practice for production systems[^7][^12]

The .NET application exports via OTLP (gRPC or HTTP), and the collector configuration handles the JSONL stdout output:

```yaml
exporters:
  file:
    path: /dev/stdout
    format: json
```


## Comparison Matrix

## Best Practices for Production

### 1. Always Use Batch Processing

Batch processing is **critical for performance** and should be configured on every collector and SDK. The batch processor groups telemetry into time-based and size-based batches, reducing network overhead and improving throughput.[^9][^13][^11][^8]

Recommended batch processor configuration for .NET applications:

- **send_batch_size**: 512-2048 for logs
- **timeout**: 1-5 seconds
- **max_queue_size**: 2048-4096

The batch processor should be placed **after** the memory_limiter and any sampling processors in the pipeline.[^13][^10]

### 2. Implement Structured Logging

Use parameterized logging instead of string interpolation to maintain structure:[^12][^14]

```csharp
// Good - Preserves structure
logger.LogInformation("User {UserId} performed {Action}", userId, action);

// Avoid - Loses structure
logger.LogInformation($"User {userId} performed {action}");
```


### 3. Include Trace Context

Always configure logs to include `traceId` and `spanId` for correlation with distributed traces. OpenTelemetry .NET automatically propagates these when properly configured:[^14][^12]

```csharp
options.IncludeFormattedMessage = true;
options.IncludeScopes = true;
options.ParseStateValues = true;
```


### 4. Configure Resource Attributes

Resource attributes provide essential context about the service and environment:[^12][^14]

```csharp
ResourceBuilder.CreateDefault()
    .AddService(serviceName: "MyService", serviceVersion: "1.0.0")
    .AddAttributes(new[]
    {
        new KeyValuePair<string, object>("environment", "production"),
        new KeyValuePair<string, object>("deployment.region", "us-east-1")
    })
```


### 5. Control Log Volume

Production environments should use appropriate log levels to prevent overwhelming the logging infrastructure:[^14][^12]

- **Production**: Information level and above
- **Development**: Debug level and above
- Consider sampling for high-traffic scenarios using tail_sampling processor


### 6. Secure Sensitive Data

Implement redaction processors to prevent logging passwords, tokens, or PII:[^12]

```yaml
processors:
  transform:
    log_statements:
      - context: log
        statements:
          - replace_pattern(body, "password=\\S+", "password=***")
```


## Recommendation

For **production deployments**, I strongly recommend **Approach 4 (OTLP Exporter + Collector)**. This architecture:[^7][^12]

1. **Decouples** application code from observability backends
2. Provides **centralized configuration** for all telemetry processing
3. Enables **rich transformations** through collector processors[^15][^8]
4. Supports **multi-tenant scenarios** with metadata-based batching[^9]
5. Allows **zero-downtime changes** to observability backends

For **quick prototyping or simple applications**, **Approach 1** (ILogger + JSON) offers the fastest path to JSONL output with minimal configuration.[^2]

The custom OTLP exporter (Approach 2) is valuable when you need OTLP compliance without running a separate collector, though it requires maintaining custom code.[^4]

## Implementation Resources

I've created comprehensive code examples and documentation:

These resources provide production-ready implementations, configuration examples, and deployment patterns for Kubernetes and Docker environments.
<span style="display:none">[^16][^17][^18][^19][^20][^21][^22][^23][^24][^25][^26][^27][^28][^29][^30][^31][^32][^33][^34][^35][^36][^37][^38][^39][^40][^41][^42][^43][^44][^45][^46][^47][^48][^49][^50][^51][^52][^53][^54]</span>

<div align="center">⁂</div>

[^1]: https://opentelemetry.io/docs/languages/dotnet/logs/getting-started-console/

[^2]: https://learn.microsoft.com/en-us/dotnet/core/extensions/console-log-formatter

[^3]: https://opentelemetry.io/docs/specs/otel/protocol/file-exporter/

[^4]: https://stackoverflow.com/questions/61363408/how-to-add-a-custom-exporter-for-capturing-traces-from-opentelemetry

[^5]: https://stackoverflow.com/questions/78162770/opentelemetry-logging-to-console-in-json-format

[^6]: https://opentelemetry.io/docs/languages/dotnet/exporters/

[^7]: https://petabridge.com/blog/easiest-opentelemetry-dotnet-otlp-collector/

[^8]: https://help.splunk.com/en/splunk-observability-cloud/manage-data/splunk-distribution-of-the-opentelemetry-collector/get-started-with-the-splunk-distribution-of-the-opentelemetry-collector/collector-components/processors/batch-processor

[^9]: https://www.dash0.com/guides/opentelemetry-batch-processor

[^10]: https://docs.dynatrace.com/docs/ingest-from/opentelemetry/collector/use-cases/batch

[^11]: https://grafana.com/docs/agent/latest/flow/reference/components/otelcol.processor.batch/

[^12]: https://www.dash0.com/knowledge/opentelemetry-logging-explained

[^13]: https://stackoverflow.com/questions/77893695/whats-the-recommended-processor-ordering-for-opentelemetry-collector-configurat

[^14]: https://www.groundcover.com/opentelemetry/opentelemetry-logs

[^15]: https://last9.io/blog/opentelemetry-processors/

[^16]: https://github.com/open-telemetry/opentelemetry-dotnet/issues/5036

[^17]: https://opentelemetry.io/docs/specs/otel/logs/

[^18]: https://www.twilio.com/en-us/blog/developers/community/build-a-logs-pipeline-in-dotnet-with-opentelemetry

[^19]: https://github.com/open-telemetry/opentelemetry-specification/issues/4056

[^20]: https://www.apollographql.com/docs/graphos/routing/observability/router-telemetry-otel/telemetry-pipelines/metrics-exporters/otlp

[^21]: https://github.com/open-telemetry/opentelemetry-dotnet/discussions/5389

[^22]: https://stackoverflow.com/questions/78661726/how-to-log-traces-in-opentelemetry-collector-to-stdout

[^23]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Console

[^24]: https://opentelemetry.io/docs/languages/sdk-configuration/otlp-exporter/

[^25]: https://learn.microsoft.com/en-us/dotnet/core/extensions/logging

[^26]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-prgrja-example

[^27]: https://opentelemetry.io/docs/languages/js/exporters/

[^28]: https://openobserve.ai/blog/opentelemetry-logging-in-dotnet-applications/

[^29]: https://lumigo.io/opentelemetry/opentelemetry-logging-how-it-works-and-4-code-examples/

[^30]: https://opentelemetry.io/docs/languages/dotnet/getting-started/

[^31]: https://opentelemetry.io/docs/specs/otel/logs/sdk_exporters/stdout/

[^32]: https://docs.redhat.com/en/documentation/red_hat_build_of_quarkus/1.11/html/configuring_logging_with_quarkus/proc-configuring-json-logging-format_quarkus-configuring-logging

[^33]: https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol

[^34]: https://www.meziantou.net/asp-net-core-json-logger.htm

[^35]: https://aws.amazon.com/blogs/developer/structured-logging-for-net-lambda/

[^36]: https://lumigo.io/blog/implementing-opentelemetry-otlp-in-net/

[^37]: https://blog.revolution.com.br/2023/07/17/log-formatters-in-c/

[^38]: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-9.0

[^39]: https://github.com/dotnet/dotnet-docker/issues/2725

[^40]: https://www.milanjovanovic.tech/blog/structured-logging-in-asp-net-core-with-serilog

[^41]: https://blog.postsharp.net/structured-logging-with-string-interpolation

[^42]: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel

[^43]: https://betterstack.com/community/guides/observability/opentelemetry-collector/

[^44]: https://aws.amazon.com/blogs/dotnet/developing-custom-processors-using-opentelemetry-in-net-8/

[^45]: https://github.com/aws-samples/dotnet-opentelemetry-samples

[^46]: https://www.reddit.com/r/dotnet/comments/e9tjmm/jsonconsole_logger_provider_implementation_for/

[^47]: https://stackoverflow.com/questions/44230373/is-there-a-way-to-format-the-output-format-in-net-core-logging

[^48]: https://www.meziantou.net/monitoring-a-dotnet-application-using-opentelemetry.htm

[^49]: https://learn.microsoft.com/en-us/shows/on-dotnet/using-the-console-log-formatter

[^50]: https://opentelemetry.io/docs/zero-code/dotnet/custom/

[^51]: https://www.nuget.org/packages/microsoft.extensions.logging.console/

[^52]: https://opentelemetry.io/docs/languages/dotnet/logs/best-practices/

[^53]: https://dateo-software.de/blog/improve-your-applications-observability-with-custom-health-checks

[^54]: https://betterstack.com/community/guides/observability/opentelemetry-best-practices/

