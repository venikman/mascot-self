## Goals
- Use OpenAI as the sole AI provider across backend and frontend.
- Remove LM Studio and Azure OpenAI provider paths, configs, and UI.
- Ensure all dependencies are on .NET 10-compatible versions and align with runtime.
- Keep observability intact and confirm end-to-end functionality.

## Package & Runtime Updates
- Remove `Azure.AI.OpenAI` from `AgentLmLocal/AgentLmLocal.csproj:10`.
- Keep `OpenAI` SDK (update to latest stable) `AgentLmLocal/AgentLmLocal.csproj:15`.
- Keep `Microsoft.Extensions.AI` `10.0.0` and `Microsoft.Agents.AI` preview lines `AgentLmLocal/AgentLmLocal.csproj:11-14`.
- Keep OpenTelemetry packages `1.9.0` and Serilog packages as-is (already .NET 10-compatible) `AgentLmLocal/AgentLmLocal.csproj:16-26,28-30`.
- Fix VS Code launch to net10.0: update `program` path to `net10.0` in `.vscode/launch.json:13`.

## Backend DI & Chat Client (Single Provider)
- Replace provider switching with a single OpenAI client.
- In `AgentLmLocal/Program.cs`:
  - Remove `AzureOpenAI` import and code paths `Program.cs:1,224-272`.
  - Delete `CreateAzureOpenAIChatClient` and helper methods `Program.cs:224-272`.
  - Rename `CreateLmStudioChatClient` → `CreateOpenAIChatClient`, defaulting to OpenAI API endpoint `https://api.openai.com/v1` and using `config.OpenAIApiKey` + `config.OpenAIModelId` `Program.cs:212-222` (repurpose with standard endpoint).
  - Simplify `CreateChatClient` to call `CreateOpenAIChatClient` only, removing the `config.Provider` switch `Program.cs:205-210`.
  - Ensure `IChatClient` registration stays `builder.Services.AddSingleton<IChatClient>(_ => CreateChatClient(config));` `Program.cs:72`.

## Configuration & Env Vars (OpenAI Only)
- Replace `AgentConfiguration` with OpenAI-only fields:
  - Remove `LlmProvider` enum `AgentLmLocal/Configuration/AgentConfiguration.cs:3-7` and `Provider` property `:11`.
  - Remove LM Studio fields `LmStudioEndpoint`, `ApiKey` (rename to `OpenAIApiKey`), `ModelId` (rename to `OpenAIModelId`) `AgentConfiguration.cs:13-18`.
  - Remove Azure fields `AgentConfiguration.cs:19-25`.
  - Add optional `OpenAIBaseUrl` (default `https://api.openai.com/v1`).
  - Update `FromEnvironment()` to read:
    - `OPENAI_API_KEY`, `OPENAI_MODEL`, optional `OPENAI_BASE_URL`.
    - Keep workflow tuning: `MINIMUM_RATING`, `MAX_ATTEMPTS` `AgentConfiguration.cs:31-68`.
- `AgentLmLocal/appsettings.json`:
  - Remove `LMSTUDIO_*` keys `appsettings.json:35-38`.
  - Optionally add non-secret model default: `OPENAI_MODEL` (e.g., `gpt-4o-mini`).
- `.env.example`:
  - Remove provider selection and LM Studio/Azure sections `/.env.example:24-44`.
  - Keep workflow vars `MINIMUM_RATING`, `MAX_ATTEMPTS` `/.env.example:50-54`.
  - Add:
    - `OPENAI_API_KEY=...`
    - `OPENAI_MODEL=gpt-4o-mini`
    - Optional `OPENAI_BASE_URL=https://api.openai.com/v1`.
  - Remove `.env.lmstudio.example` and `.env.azureopenai.example` files.

## Frontend UI & Client
- No provider/model selection UI exists; no visual changes required.
  - Verified in `AgentLmLocal/ClientApp/src/components/*.tsx` and `src/services/chatApi.ts:10-26`.
- Ensure chat still posts `{ message }` to `/chat` and works with backend `Program.cs:151-175`.

## Codebase Adjustments
- Update using directives:
  - Remove `using Azure.AI.OpenAI;` from `Program.cs:1`.
  - Retain `using OpenAI;` and `using Microsoft.Extensions.AI;`.
- Remove dead code for Azure parsing (`TryParseServiceVersion`, `CreateAzureOpenAIOptions`) `Program.cs:242-272,257-272`.
- Keep agents/services unchanged; they rely on `IChatClient` and remain provider-agnostic:
  - `AgentFactory.cs:7-35`, `LlmService.cs:6-27`.

## .NET 10 Compatibility & Performance
- Confirm `TargetFramework=net10.0` `AgentLmLocal/AgentLmLocal.csproj:4`.
- Ensure packages align with .NET 10 and latest SDKs:
  - `OpenAI` SDK latest stable.
  - `Microsoft.Extensions.AI` `10.0.0`.
  - `Microsoft.Extensions.AI.OpenAI` latest compatible (prefer stable; otherwise latest preview matching `10.0.0`).
- Retain OpenTelemetry configuration and Serilog JSONL logging (`Program.cs:46-68`, `AgentLmLocal/appsettings.json:2-28`).

## Verification & Tests
- Build backend: `dotnet build` and run: `dotnet run`.
- Environment setup:
  - Set `OPENAI_API_KEY` and `OPENAI_MODEL` (optional `OPENAI_BASE_URL`).
- API checks:
  - `POST /chat` with `{ message: "Hello" }` returns OpenAI response `Program.cs:151-175`.
  - `POST /run` with `{ task: "Create a mascot slogan" }` runs full workflow and logs events; poll `GET /runs/{id}` for status `Program.cs:177-200`.
- Frontend checks:
  - `bun run dev` then interact via `http://localhost:5173`; chat UI works with OpenAI; telemetry posts to `/otel/traces`.
- Observability checks:
  - Console shows compact JSON logs with Trace/Span enrichment.
  - OTEL spans ingested by `/otel/traces` without errors `Program.cs:89-148`.

## Deliverables
- Cleaned csproj with only OpenAI provider packages.
- Simplified `Program.cs` DI to OpenAI-only.
- `AgentConfiguration` refactored to OpenAI-only envs.
- Updated `appsettings.json` and `.env.example` to OpenAI settings.
- Removed LM Studio/Azure configs and examples.
- Fixed launch config for net10.0.
- Verified end-to-end operation with OpenAI on .NET 10.

## Notes
- Secrets: do not store API keys in `appsettings.json`; use environment variables.
- If you prefer a different default `OPENAI_MODEL` (e.g., `gpt-4o`), specify it before implementation.
