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