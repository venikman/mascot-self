using System.Text.Json;
using Microsoft.Agents.AI;

namespace AgentLmLocal.Services;

/// <summary>
/// Service for invoking LLM operations with structured outputs.
/// </summary>
public sealed class LlmService
{
    /// <summary>
    /// Invokes an LLM with a prompt and deserializes the response to a structured type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to.</typeparam>
    /// <param name="agent">The AI agent to invoke.</param>
    /// <param name="thread">The agent thread to use.</param>
    /// <param name="prompt">The prompt to send to the LLM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<T> InvokeStructuredAsync<T>(
        AIAgent agent,
        AgentThread thread,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var result = await agent.RunAsync(prompt, thread, cancellationToken: cancellationToken);
        return JsonSerializer.Deserialize<T>(result.Text)!;
    }

    /// <summary>
    /// Invokes an LLM with a prompt and returns the raw text response.
    /// </summary>
    /// <param name="agent">The AI agent to invoke.</param>
    /// <param name="thread">The agent thread to use.</param>
    /// <param name="prompt">The prompt to send to the LLM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw text response.</returns>
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
