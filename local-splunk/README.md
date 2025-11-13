How To Proceed

- Start Splunk:
  - docker compose -f local-splunk/docker-compose.yml up -d
  - Wait until: docker logs -f splunk | grep 'The engine is now running.'
- Enable HEC and get token:
  - chmod +x local-splunk/setup-hec.sh
  - local-splunk/setup-hec.sh
  - It will output:
    - export SPLUNK_HEC_URL=https://localhost:8088/services/collector/event
    - export SPLUNK_HEC_TOKEN=<token>
- Send events:
  - chmod +x local-splunk/post-sample-events.sh
  - ASPNETCORE_ENVIRONMENT=Development dotnet run --project AgentHello | tee agenthello.log
  - `curl -s http://localhost:5052/health && echo && curl -s -X POST http://localhost:5052/chat -H "Content-Type: application/json" -d '{"text":"Hello, world"}'`
  - local-splunk/post-sample-events.sh agenthello.log

Search in Splunk
- UI: http://localhost:8000 (user admin, password changeme123!  

Search Examples (SPL)
- Requests by status: index=main sourcetype=_json source=AgentHello | stats count by StatusCode
- Latency by route: index=main sourcetype=_json SourceContext="Serilog.AspNetCore.RequestLoggingMiddleware" | stats avg(Elapsed) by RequestPath
- Trace correlation: index=main sourcetype=_json TraceId=be4b67b2362fb577af81f3fda56541d3 | table _time TraceId SpanId RequestPath StatusCode Elapsed  

index=main sourcetype=_json source=AgentHello | table _time @m TraceId SpanId RequestPath StatusCode Elapsed




