# Serilog as OTLP Bridge

## Overview
Use a Serilog OpenTelemetry sink (or Splunk-specific OTLP sink) so console logs remain available while the same events are converted into OTLP envelopes. This keeps instrumentation untouched: everything is driven by Serilog configuration.

## When To Use
- You want OTLP compatibility but prefer to manage routing via Serilog rather than OpenTelemetry SDK exporters.
- Incremental migrations where log format stability matters more than span fidelity.

## Considerations
- Sink maturity varies; batching/retry semantics may differ from native OpenTelemetry exporters.
- Harder to share exporter configuration between logs and traces, which may lead to drift.
- Requires monitoring two pipelines (Serilog sinks + OpenTelemetry tracing) for health.
