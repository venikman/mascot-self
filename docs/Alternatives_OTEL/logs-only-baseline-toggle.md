# Logs-Only Baseline Toggle

## Overview
Operate exclusively through stdout/stderr JSON logs enriched with OpenTelemetry span metadata. A configuration flag (e.g., `Telemetry:Mode=LogsOnly`) keeps exporters disabled while still producing compliant trace identifiers for downstream log processors such as Splunk or ELK.

## When To Use
- Local prototyping or developer workstations without collectors.
- Environments where log shipping infrastructure already centralizes observability data.
- Experiments focused on reconstructing traces from logs without adding exporters.

## Considerations
- Span data never leaves the process, so cross-service correlation depends entirely on log fidelity.
- Sampling, batching, and back-pressure controls are limited to whatever the log shipper provides.
- Upgrading to exporters later requires coordinating configuration across services unless a toggle is already in place.
