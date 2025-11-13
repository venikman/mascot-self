#!/usr/bin/env bash
set -euo pipefail

HEC_URL=${SPLUNK_HEC_URL:-https://localhost:8088/services/collector/event}
HEC_TOKEN=${SPLUNK_HEC_TOKEN:-}

if [[ -z "${HEC_TOKEN}" ]]; then
  echo "SPLUNK_HEC_TOKEN is required. Export it first."
  exit 1
fi

FILE=${1:-}

if [[ -n "${FILE}" ]]; then
  tail -n 20 "${FILE}" | while read -r line; do
    payload=$(jq -nc --argjson event "${line}" '{event: $event, sourcetype: "_json", source: "AgentHello", host: "local", index: "main"}')
    curl -s -k "${HEC_URL}" -H "Authorization: Splunk ${HEC_TOKEN}" -H "Content-Type: application/json" -d "${payload}" >/dev/null
  done
  echo "Posted last 20 events from ${FILE}"
else
  sample=$(jq -nc '{"@m":"HTTP POST /chat responded 200","TraceId":"trace123","SpanId":"span123","RequestPath":"/chat","StatusCode":200,"Elapsed":123.4,"service.name":"AgentHello","deployment.environment":"Development"}')
  payload=$(jq -nc --argjson event "${sample}" '{event: $event, sourcetype: "_json", source: "AgentHello", host: "local", index: "main"}')
  curl -s -k "${HEC_URL}" -H "Authorization: Splunk ${HEC_TOKEN}" -H "Content-Type: application/json" -d "${payload}"
  echo "Posted one sample event"
fi

