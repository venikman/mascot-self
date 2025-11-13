# Testing Setup

## appsettings.json
- `AI.BaseUrl` (default `http://127.0.0.1:1234/v1`)
- `AI.ApiKey` (required)
- `AI.Model` (default `kat-dev-mlx`)
- `AI.Version` (optional)
- `Workflow.MinimumRating` (default `7`)
- `Workflow.MaxAttempts` (default `3`)

## Verify
```bash
curl http://localhost:5000/health
curl -X POST http://localhost:5000/chat -H "Content-Type: application/json" -d '{"message":"Hello"}'
```

## User Secrets (dotnet)
- Ensure the project has a user secrets ID. This repo sets it in `AgentHello/AgentHello.csproj:7`.
- Secrets are loaded in development via `AddUserSecrets<Program>()` in `AgentHello/Program.cs:14`.

### Set secrets
```bash
cd AgentHello
dotnet user-secrets init
dotnet user-secrets set "AI:ApiKey" "<your-openai-key>"
dotnet user-secrets set "AI:Model" "gpt-4o-mini"
dotnet user-secrets set "AI:Endpoint" "https://api.openai.com/v1"
```

### Inspect / remove
```bash
dotnet user-secrets list
dotnet user-secrets remove "AI:ApiKey"
dotnet user-secrets clear
```

### Environment overrides
- Hierarchical keys: `AI__ApiKey`, `AI__Model`, `AI__Endpoint`
- Flat keys also work: `OPENAI_API_KEY`, `AI_API_KEY`, `AI_MODEL`, `AI_BASE_URL`
