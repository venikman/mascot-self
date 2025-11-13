# OpenTelemetry Logging Provider

## Overview
Register `builder.Logging.AddOpenTelemetry(...)` so structured Serilog events flow through OTLP alongside traces/metrics. Serilog can remain for local console output while OpenTelemetry exporters ship logs, ensuring a single configuration surface for pipelines.

## When To Use
- Teams seeking full OTLP parity (logs + traces + metrics) without managing separate sinks per logger.
- Deployments that already standardize on OpenTelemetry collectors or managed backends.

## Considerations
- Adds more moving parts during local development (log exporters, credentials, collector availability).
- Requires careful filtering to prevent double emission when other sinks remain enabled.
- Costs/quotas increase because every log line flows through collectors.
