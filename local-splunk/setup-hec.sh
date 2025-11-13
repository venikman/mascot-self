#!/usr/bin/env bash
set -euo pipefail

if ! docker ps --format '{{.Names}}' | grep -q '^splunk$'; then
  echo "Splunk container is not running. Start it with: docker compose -f local-splunk/docker-compose.yml up -d"
  exit 1
fi

ADMIN_PASS=${SPLUNK_ADMIN_PASSWORD:-changeme123!}

echo "Waiting for Splunk management API..."
for i in {1..30}; do
  code=$(curl -sk -u admin:"${ADMIN_PASS}" -o /dev/null -w '%{http_code}' https://localhost:8089/services/server/info || true)
  if [[ "$code" == "200" ]]; then
    break
  fi
  sleep 2
done

echo "Enabling HEC globally via REST..."
curl -sk -u admin:"${ADMIN_PASS}" https://localhost:8089/servicesNS/nobody/splunk_httpinput/data/inputs/http/http -X POST -d disabled=0 -d output_mode=json >/dev/null

echo "Ensuring token 'agenthello' exists..."
exists=$(curl -sk -u admin:"${ADMIN_PASS}" https://localhost:8089/servicesNS/nobody/splunk_httpinput/data/inputs/http/agenthello?output_mode=json -o /dev/null -w '%{http_code}')
if [[ "$exists" != "200" ]]; then
  curl -sk -u admin:"${ADMIN_PASS}" https://localhost:8089/servicesNS/nobody/splunk_httpinput/data/inputs/http -X POST \
    -d name=agenthello -d index=main -d sourcetype=_json -d disabled=0 -d output_mode=json >/dev/null
fi

TOKEN=$(curl -sk -u admin:"${ADMIN_PASS}" https://localhost:8089/servicesNS/nobody/splunk_httpinput/data/inputs/http/agenthello?output_mode=json | jq -r '.entry[0].content.token')

echo "Export these variables to send events:"
echo "export SPLUNK_HEC_URL=https://localhost:8088/services/collector/event"
echo "export SPLUNK_HEC_TOKEN=${TOKEN}"
