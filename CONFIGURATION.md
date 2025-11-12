# Testing Setup

## Quick Start
```bash
cp .env.example .env
dotnet run
```

## Environment Variables
- `OPENAI_BASE_URL` (default `http://127.0.0.1:1234/v1`)
- `OPENAI_API_KEY` (required)
- `OPENAI_MODEL` (default `kat-dev-mlx`)
- `OPENAI_VERSION` (optional)
- `MINIMUM_RATING` (default `7`)
- `MAX_ATTEMPTS` (default `3`)

## Verify
```bash
curl http://localhost:5000/health
curl -X POST http://localhost:5000/chat -H "Content-Type: application/json" -d '{"message":"Hello"}'
curl -X POST http://localhost:5000/run -H "Content-Type: application/json" -d '{"task":"Create a mascot slogan"}'
curl http://localhost:5000/runs/{workflowId}
```

## Troubleshooting
- Connection refused: ensure local endpoint on `127.0.0.1:1234` and `curl http://127.0.0.1:1234/v1/models`
- 401 Unauthorized: check `OPENAI_API_KEY`
- 404 Model not found: verify `OPENAI_MODEL` availability
