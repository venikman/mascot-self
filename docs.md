dotnet add package Microsoft.Agents.AI.AzureOpenAI --prerelease
dotnet add package Azure.AI.OpenAI


export AZURE_OPENAI_ENDPOINT="https://<resource>.openai.azure.com/"
export AZURE_OPENAI_API_KEY="<key>"
export AZURE_OPENAI_DEPLOYMENT="<deploymentName>"
export AZURE_OPENAI_API_VERSION="2024-10-21"

Assumptions. .NET 9, LM Studio server at :1234, model ID matches /v1/models.
Tests. curl /v1/models returns IDs; dotnet run prints one-liner.
Risks. If /v1/responses is unavailable in your build/model, the fallback path handles it.
Next. Add a trivial tool (e.g., now() function) and a second agent to see handoff behavior.

## AgentThread sample

Reference doc: https://learn.microsoft.com/en-us/agent-framework/user-guide/agents/multi-turn-conversation?pivots=programming-language-csharp

`AgentLmLocal/Program.cs` now demos both threading usage patterns the doc calls out:

1. `agent.GetNewThread()` gives you a durable thread you can pass to every `RunAsync` call to keep the model's short-term memory intact.
2. `thread.Serialize(ThreadSerializerOptions)` + `agent.DeserializeThread(payload, ThreadSerializerOptions)` show how to persist and resume conversations regardless of whether the underlying service stores state server-side (Foundry/Responses) or client-side (chat completions).

Run it with `dotnet run --project AgentLmLocal` (pointing `OPENAI_BASE_URL`, `OPENAI_API_KEY`, and `OPENAI_MODEL` at LM Studio or any OpenAI-compatible host) and you'll see output like:

```csharp
AgentThread thread = agent.GetNewThread();
await agent.RunAsync("Remember this exact phrase: sharp mascot thread sample.", thread);
await agent.RunAsync("What did I just ask you to remember?", thread);

JsonElement serializedThread = thread.Serialize(ThreadSerializerOptions);
AgentThread resumedThread = agent.DeserializeThread(serializedThread, ThreadSerializerOptions);
await agent.RunAsync("Resume the saved conversation and suggest the next action.", resumedThread);
```

`ThreadSerializerOptions` is a cached `new JsonSerializerOptions(JsonSerializerDefaults.Web)` so the serialization format stays consistent between save/restore.
