#!/usr/bin/env bash
set -euo pipefail

# Port conflict resolver for Aspire dashboard and agent service
# Targets:
#  - Aspire Dashboard HTTP proxy: 15045
#  - Aspire Dashboard HTTPS: 17021
#  - Aspire Dashboard OTLP gRPC proxy: 21044
#  - Agent service HTTP: 5088

PORTS=(15045 17021 21044 5088)

print() { printf "%s\n" "$1"; }
print_info() { printf "[INFO] %s\n" "$1"; }
print_success() { printf "[SUCCESS] %s\n" "$1"; }
print_warn() { printf "[WARN] %s\n" "$1"; }
print_error() { printf "[ERROR] %s\n" "$1"; }

cleanup_port() {
  local port="$1"
  # macOS: use lsof to find listeners
  local pids
  pids=$(lsof -n -iTCP:"$port" -sTCP:LISTEN -t 2>/dev/null || true)
  if [[ -z "$pids" ]]; then
    print_info "Port $port is free"
    return 0
  fi
  print_warn "Port $port is in use by PID(s): $pids"
  for pid in $pids; do
    print_info "Attempting to terminate PID $pid (port $port)"
    if kill "$pid" 2>/dev/null; then
      sleep 0.3
      if kill -0 "$pid" 2>/dev/null; then
        print_warn "PID $pid still running; sending SIGKILL"
        kill -9 "$pid" 2>/dev/null || true
      fi
    else
      print_warn "Could not gracefully kill PID $pid; trying SIGKILL"
      kill -9 "$pid" 2>/dev/null || true
    fi
  done
  # Re-check
  if lsof -n -iTCP:"$port" -sTCP:LISTEN -t >/dev/null 2>&1; then
    print_error "Port $port still in use after cleanup"
    return 1
  else
    print_success "Port $port is now free"
    return 0
  fi
}

print_info "Resolving port conflicts for Aspire and agent"
for p in "${PORTS[@]}"; do
  cleanup_port "$p" || true
done

print_success "Port resolution completed"