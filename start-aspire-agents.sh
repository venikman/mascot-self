#!/usr/bin/env bash
set -euo pipefail

echo " 🚀 Starting .NET Aspire Agentic Demo with Tracing... "
echo " ================================================== "

# Colors
YELLOW='\033[1;33m'
GREEN='\033[1;32m'
RED='\033[1;31m'
NC='\033[0m'

function info() { echo -e "${YELLOW}[INFO]${NC} $1"; }
function success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
function warn() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
function error() { echo -e "${RED}[ERROR]${NC} $1"; }

root_dir="$(cd "$(dirname "$0")" && pwd)"

info "Checking prerequisites..."

if ! command -v dotnet >/dev/null 2>&1; then
  error "dotnet SDK not found. Install .NET 9 SDK first: https://dotnet.microsoft.com/download"
  exit 1
fi

sdk_ver=$(dotnet --version)
success ".NET SDK version: ${sdk_ver}"

# Check Aspire workload
if ! dotnet workload list | grep -q "aspire"; then
  warn "Aspire workload not found. Attempting installation..."
  if sudo -n true 2>/dev/null; then
    info "Installing Aspire workload with sudo..."
    if ! sudo dotnet workload install aspire; then
      warn "Aspire workload installation failed. You can install manually later."
    else
      success "Aspire workload installed."
    fi
  else
    warn "No sudo privileges detected. Skipping automatic installation."
    echo ""
    echo "To install manually (recommended):"
    echo "  1) Run: sudo dotnet workload install aspire"
    echo "  2) Or run this script with elevated privileges"
    echo ""
  fi
fi

info "Restoring solution packages"
dotnet restore "${root_dir}/AgentLmLocal.sln"

info "Building AppHost"
dotnet build -c Debug "${root_dir}/AgentLmLocal.AppHost/AgentLmLocal.AppHost.csproj"

info "Resolving known port conflicts (dashboard and agent)"
bash "${root_dir}/resolve-port-conflicts.sh" || true

info "Starting Aspire AppHost (Redis, Postgres, Agent)"
export ASPNETCORE_URLS="http://0.0.0.0:5088"
export OTEL_SERVICE_NAME="agentlm-local"
export ASPIRE_ALLOW_UNSECURED_TRANSPORT=true

info "Opening helpful endpoints after startup:"
echo "  - Agent endpoint: http://localhost:5088/health"
echo "  - Trigger run:   curl -X POST http://localhost:5088/run"
echo "  - Aspire dashboard opens automatically (if installed)"

dotnet run --launch-profile http --project "${root_dir}/AgentLmLocal.AppHost/AgentLmLocal.AppHost.csproj"