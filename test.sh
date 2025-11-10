#!/usr/bin/env bash
set -euo pipefail

# Simple sanity checks for local agent and Aspire

root_dir="$(cd "$(dirname "$0")" && pwd)"

function info() { echo "[INFO] $1"; }
function ok() { echo "[OK] $1"; }
function fail() { echo "[FAIL] $1"; exit 1; }

agent_url="http://localhost:5088"
health_endpoint="$agent_url/health"
run_endpoint="$agent_url/run"

info "Checking agent health: $health_endpoint"
if curl -sSf "$health_endpoint" >/dev/null; then
  ok "Agent health endpoint is reachable"
else
  fail "Agent health check failed"
fi

info "Triggering demo run: POST $run_endpoint"
if curl -sSf -X POST "$run_endpoint" -H 'Content-Type: application/json' -d '{}' >/dev/null; then
  ok "Run triggered successfully"
else
  fail "Run trigger failed"
fi

info "If Aspire is running, open the dashboard at http://localhost:15045"