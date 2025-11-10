# .NET Aspire Agentic Demo (Streamlined)

A compact multi-agent demo with .NET Aspire and OpenTelemetry. Minimal scripts, clear endpoints, easy local runs.

## Quick Start

- Make scripts executable:
  ```bash
  chmod +x start-aspire-agents.sh resolve-port-conflicts.sh run-agent-standalone.sh test.sh
  ```
- Start Aspire stack (Redis, Postgres, Agent):
  ```bash
  ./start-aspire-agents.sh
  ```
- Dashboard: `http://localhost:15045`
- Agent:
  - Health: `http://localhost:5088/health`
  - Trigger run: `curl -X POST http://localhost:5088/run`
- Simple checks:
  ```bash
  ./test.sh
  ```

## Standalone Agent (no Aspire)

- Start only the agent web server:
  ```bash
  ./run-agent-standalone.sh
  ```
- Then:
  ```bash
  curl http://localhost:5088/health
  curl -X POST http://localhost:5088/run
  ```

## Monitoring

- The agent exports traces, metrics, and logs via OTLP when the Aspire dashboard is running.
- Primary dashboard URL: `http://localhost:15045`

## Troubleshooting

- Free up ports before starting:
  ```bash
  ./resolve-port-conflicts.sh
  ```
- If HTTPS profile causes HTTP/2 negotiation errors, use the HTTP profile (default in `start-aspire-agents.sh`).

## Notes

- Agent service is bound directly to `http://0.0.0.0:5088` and reachable at `http://localhost:5088`.
- Minimal script set retained:
  - `start-aspire-agents.sh` — main entry point
  - `resolve-port-conflicts.sh` — frees dashboard and agent ports
  - `run-agent-standalone.sh` — fallback when Aspire isn’t available
  - `test.sh` — quick health and run checks

Happy building!