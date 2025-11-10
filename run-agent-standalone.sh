#!/usr/bin/env bash
set -euo pipefail

echo " ▶️  Starting Agent standalone (without Aspire) "
echo " ============================================= "

root_dir="$(cd "$(dirname "$0")" && pwd)"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "[ERROR] dotnet SDK not found. Install .NET 9 SDK first: https://dotnet.microsoft.com/download"
  exit 1
fi

export ASPNETCORE_URLS="http://0.0.0.0:5088"
export OTEL_SERVICE_NAME="agentlm-local"
export ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL="http://localhost:19149"

dotnet restore "${root_dir}/AgentLmLocal/AgentLmLocal.csproj"
dotnet build -c Debug "${root_dir}/AgentLmLocal/AgentLmLocal.csproj"

echo "[INFO] Agent server starting on http://localhost:5088"
echo "[TIP] Trigger run: curl -X POST http://localhost:5088/run"
dotnet run --project "${root_dir}/AgentLmLocal/AgentLmLocal.csproj"