# ADR 0001: Logs-Only Telemetry Strategy

## Status
Accepted

## Context
The prototype evaluates whether OpenTelemetry-compliant data can be captured entirely through stdout/stderr logs enriched with span metadata. Introducing OTLP exporters or additional sinks adds cognitive overhead, complicates local setup, and distracts from the primary learning goal of reconstructing traces from logs.

## Decision
Keep telemetry in `LogsOnly` mode by default. The application enriches Serilog console output with trace/span identifiers and relies on downstream log shippers (e.g., Splunk HEC) for aggregation. Exporters remain disabled, but the configuration/extension points stay in place so that future experiments can enable OTLP without reworking core code.

## Consequences
- **Pros:** Minimal infrastructure required; developers can run the prototype with just `dotnet run` and log tailing. Telemetry data mirrors production-style attributes while avoiding collector dependencies.
- **Cons:** Cross-service correlation requires parsing logs, and real-time metrics/traces are unavailable until exporters are toggled on. Scaling beyond a prototype will need additional investment in collectors and pipeline hardening.
