using System.Text.Json;
using Microsoft.Agents.AI;

namespace AgentLmLocal.Services;

public sealed class LlmService
{
    public async Task<T> InvokeStructuredAsync<T>(
        AIAgent agent,
        AgentThread thread,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var result = await agent.RunAsync(prompt, thread, cancellationToken: cancellationToken);
        return JsonSerializer.Deserialize<T>(result.Text)!;
    }

    public async Task<string> InvokeAsync(
        AIAgent agent,
        AgentThread thread,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var result = await agent.RunAsync(prompt, thread, cancellationToken: cancellationToken);
        return result.Text;
    }
}
