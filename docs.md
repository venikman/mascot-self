# Agent middleware walkthrough

This repo mirrors the [official Agent Framework middleware guide](https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/agent-middleware?pivots=programming-language-csharp) with a runnable sample under `AgentLmLocal`. The sample wires up all three middleware layers—agent run, function invocation, and `IChatClient`—so you can see how each hook behaves without needing any hosted model.

## Run the sample

```bash
dotnet run --project AgentLmLocal
```

`AgentLmLocal/Program.cs` prints only the final assistant answer so it can double as a scripting sample. The middleware hooks remain in place so you can drop in your own instrumentation:

- `AgentRunLoggerMiddleware` intercepts every `RunAsync` call (wired via `.Use(..., runStreamingFunc: null)`), making it easy to add tracing, retries, or guardrails around agent execution.
- `FunctionInvocationLoggerMiddleware` shows how to edit arguments before tool execution; it auto-fills a missing `location` argument to keep the demo deterministic and could be extended with validation or caching.
- `ChatClientLoggerMiddleware` wraps `IChatClient.GetResponseAsync(...)`, demonstrating how to insert telemetry or request shaping before the LLM call. Right now it simply forwards to the inner client to keep the console clean.

Add any `Console.WriteLine` statements you want inside those middleware functions for debugging—they are the same extension points highlighted in the docs.

## Mapping to the docs

| Doc concept | Where it shows up here |
|-------------|------------------------|
| Agent run middleware | `AgentRunLoggerMiddleware` in `AgentLmLocal/Program.cs` mirrors the sample `CustomAgentRunMiddleware` and demonstrates inspecting both the `messages` snapshot and the `AgentRunResponse`. |
| Function calling middleware | `FunctionInvocationLoggerMiddleware` shows how to call `next(context, cancellationToken)` and log/modify arguments. You could set `context.Terminate = true` before returning to short-circuit execution, matching the warning in the docs. |
| `IChatClient` middleware | `ChatClientLoggerMiddleware` is added through `DemoChatClient.AsBuilder().Use(...)`. This is the same builder described in the article, so you can swap in Azure OpenAI, OpenAI, or LM Studio clients without code changes. |

Because the demo uses a fake chat client and tool, you can edit the middleware bodies freely and observe the results with zero cost.

## Extend the example

1. Replace `DemoChatClient` with your real `IChatClient` (OpenAI, Azure OpenAI, LM Studio, etc.) and the middleware will keep logging.
2. Add guards or observability (metrics, retries) inside the middleware bodies to turn the logging example into production-ready behaviors.
3. Experiment with terminating the tool loop by toggling `context.Terminate` inside `FunctionInvocationLoggerMiddleware` to see how it affects the thread, just as the warning in the docs explains.

If you need to reference the raw code, open `AgentLmLocal/Program.cs`—the middleware functions live at the bottom of the file so you can copy/paste them into other agents.

## Agents in workflows (LM Studio)

`WorkflowLmLocal` mirrors the [agents in workflows tutorial](https://learn.microsoft.com/en-us/agent-framework/tutorials/workflows/agents-in-workflows?pivots=programming-language-csharp) but swaps Azure Foundry for a local LM Studio OpenAI endpoint. Three `ChatClientAgent` instances (French, Spanish, English) are wired in a sequential workflow graph and executed with streaming updates, exactly like the tutorial describes but without Azure identity.

### Run the sample

1. Launch LM Studio's local server with an OpenAI-compatible chat completion endpoint (defaults to `http://127.0.0.1:1234/v1`).
2. Optionally override any of these environment variables:
   - `LM_STUDIO_BASE_URL` – base URL to the LM Studio server (default `http://127.0.0.1:1234/v1/`).
   - `LM_STUDIO_MODEL_ID` – model name exposed by LM Studio (default `lmstudio-community/Meta-Llama-3-8B-Instruct`).
   - `LM_STUDIO_API_KEY` – LM Studio API key if you enabled auth (default `lm-studio` to match the server's placeholder).
3. Run the workflow (you can pass custom input text as CLI args):

   ```bash
   dotnet run --project WorkflowLmLocal -- "Translate this sample all the way back to English."
   ```

The console prints the initial prompt, streamed workflow events (one per translator), and a final completion message once the turn token finishes propagating.
