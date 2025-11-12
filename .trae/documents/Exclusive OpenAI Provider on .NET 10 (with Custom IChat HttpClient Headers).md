## Goals
- Use OpenAI as the sole AI provider across backend and frontend.
- Remove LM Studio and Azure OpenAI provider paths, configs, and UI.
- Ensure all dependencies are .NET 10-compatible and align with runtime.
- Create the `IChatClient` using an `HttpClient` that sets two custom headers on every request: `api_key` and `version`.
- Keep observability intact and confirm end-to-end functionality.

## Package & Runtime Updates
- Remove `Azure.AI.OpenAI` from `AgentLmLocal/AgentLmLocal.csproj:10`.
- Keep `OpenAI` SDK (update to latest stable) `AgentLmLocal/AgentLmLocal.csproj:15`.
- Keep `Microsoft.Extensions.AI` `10.0.0` and `Microsoft.Agents.AI` preview lines `AgentLmLocal/AgentLmLocal.csproj:11-14`.
- Keep OpenTelemetry packages `1.9.0` and Serilog packages (already .NET 10-compatible) `AgentLmLocal/AgentLmLocal.csproj:16-26,28-30`.
- Fix VS Code launch to net10.0: update `program` path to `net10.0` in `.vscode/launch.json:13`.

## Backend DI & Chat Client (Single Provider + Custom Headers)
- Register a named `HttpClient` with required defaults:
  - `builder.Services.AddHttpClient("openai", client => { client.BaseAddress = new Uri(config.OpenAIBaseUrl); client.DefaultRequestHeaders.Add("api_key", config.OpenAIApiKey); client.DefaultRequestHeaders.Add("version", config.OpenAIVersion); });`
- Create the OpenAI client using this `HttpClient` via transport:
  - Build `OpenAIClientOptions` (or `OpenAI.OpenAIClientOptions`) with `Endpoint = new Uri(config.OpenAIBaseUrl)` and `Transport = new HttpClientTransport(httpClient)`.
  - Instantiate `OpenAIClient` with `new ApiKeyCredential(config.OpenAIApiKey)`.
  - Adapt to `IChatClient` with `.GetChatClient(config.OpenAIModelId).AsIChatClient()`.
- Simplify DI:
  - Replace provider switching with a single factory that pulls `IHttpClientFactory` to create the named client and constructs the above `IChatClient` `AgentLmLocal/Program.cs:69-73,205-221`.
- Remove Azure path and helpers (`CreateAzureOpenAIChatClient`, version parsing) `AgentLmLocal/Program.cs:224-272`.

## Configuration & Env Vars (OpenAI Only)
- `AgentConfiguration`:
  - Remove `LlmProvider` enum and `Provider` property `AgentLmLocal/Configuration/AgentConfiguration.cs:3-7,11`.
  - Replace LM Studio fields with `OpenAIApiKey`, `OpenAIModelId`, `OpenAIBaseUrl` (default `https://api.openai.com/v1`), and `OpenAIVersion` (used for the custom header `version`).
  - Update `FromEnvironment()` to read:
    - `OPENAI_API_KEY`, `OPENAI_MODEL`, optional `OPENAI_BASE_URL`, optional `OPENAI_VERSION`.
    - Keep workflow tuning: `MINIMUM_RATING` and `MAX_ATTEMPTS` `AgentLmLocal/Configuration/AgentConfiguration.cs:31-68`.
- `AgentLmLocal/appsettings.json`:
  - Remove `LMSTUDIO_*` keys `AgentLmLocal/appsettings.json:35-38`.
  - Optionally add non-secret model default: `OPENAI_MODEL`.
- `.env.example`:
  - Remove provider selection and LM Studio/Azure sections `/.env.example:24-44`.
  - Keep `MINIMUM_RATING`, `MAX_ATTEMPTS` `/.env.example:50-54`.
  - Add:
    - `OPENAI_API_KEY=...`
    - `OPENAI_MODEL=gpt-4o-mini` (example)
    - Optional `OPENAI_BASE_URL=https://api.openai.com/v1`
    - Optional `OPENAI_VERSION=2024-10-21` (example value for custom header)
  - Remove `.env.lmstudio.example` and `.env.azureopenai.example` files.

## Frontend UI & Client
- No provider/model selection UI exists; no visual changes required.
  - Verified in `AgentLmLocal/ClientApp/src/components/*.tsx` and `src/services/chatApi.ts:10-26`.
- Chat continues to post `{ message }` to `/chat` and works with backend `AgentLmLocal/Program.cs:151-175`.

## Codebase Adjustments
- Update using directives:
  - Remove `using Azure.AI.OpenAI;` `AgentLmLocal/Program.cs:1`.
  - Retain `using OpenAI;` and `using Microsoft.Extensions.AI;`.
- Remove dead Azure code (`CreateAzureOpenAIOptions`, `TryParseServiceVersion`) `AgentLmLocal/Program.cs:242-272,257-272`.
- Keep agents/services unchanged; they rely on `IChatClient` and remain provider-agnostic:
  - `AgentFactory.cs:7-35`, `LlmService.cs:6-27`.

## .NET 10 Compatibility & Performance
- Confirm `TargetFramework=net10.0` `AgentLmLocal/AgentLmLocal.csproj:4`.
- Ensure packages align with .NET 10 and latest SDKs:
  - `OpenAI` SDK at latest stable.
  - `Microsoft.Extensions.AI` `10.0.0` and `Microsoft.Extensions.AI.OpenAI` matching version.
- Retain OpenTelemetry configuration and Serilog JSONL logging (`AgentLmLocal/Program.cs:46-68`, `AgentLmLocal/appsettings.json:2-28`).

## Verification & Tests
- Build backend and run: `dotnet build` / `dotnet run`.
- Environment setup:
  - Set `OPENAI_API_KEY`, `OPENAI_MODEL`; optional `OPENAI_BASE_URL`, `OPENAI_VERSION`.
- Header injection verification:
  - Create an integration test (or dev harness) that sets `OPENAI_BASE_URL` to a local test endpoint or injects a custom `HttpMessageHandler` capturing requests to assert `api_key` and `version` headers are present.
  - Confirm OpenAI calls still authenticate via SDK credentials and include the custom headers.
- API checks:
  - `POST /chat` returns OpenAI response `AgentLmLocal/Program.cs:151-175`.
  - `POST /run` runs full workflow; `GET /runs/{id}` returns status `AgentLmLocal/Program.cs:177-200`.
- Frontend checks:
  - `bun run dev` → interact at `http://localhost:5173`; chat works; telemetry posts to `/otel/traces`.
- Observability checks:
  - Console shows compact JSON logs; OTEL spans ingested by `/otel/traces` without errors `AgentLmLocal/Program.cs:89-148`.

## Deliverables
- Cleaned csproj to only include OpenAI provider packages.
- Simplified `Program.cs` DI to OpenAI-only and using a named `HttpClient` with `api_key` and `version` headers.
- `AgentConfiguration` refactored to OpenAI-only envs including `OPENAI_VERSION`.
- Updated `appsettings.json` and `.env.example` to OpenAI settings.
- Removed LM Studio/Azure configs and examples.
- Fixed launch config for net10.0.
- Verified end-to-end operation with OpenAI on .NET 10, including header injection.

## Notes
- Do not log secrets (e.g., `OPENAI_API_KEY`). Keep headers applied via `HttpClient` but never included in logs.
- If you prefer specific defaults for `OPENAI_MODEL` or `OPENAI_VERSION`, provide them before implementation.