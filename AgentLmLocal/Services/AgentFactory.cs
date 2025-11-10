using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace AgentLmLocal.Services;

/// <summary>
/// Factory for creating AI agents with consistent configuration.
/// </summary>
public sealed class AgentFactory
{
    private readonly IChatClient _chatClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentFactory"/> class.
    /// </summary>
    /// <param name="chatClient">The chat client to use for all agents.</param>
    public AgentFactory(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    /// <summary>
    /// Creates an AI agent with the specified configuration.
    /// </summary>
    /// <param name="instructions">The system instructions for the agent.</param>
    /// <param name="responseFormat">Optional response format for structured outputs.</param>
    /// <returns>A tuple containing the agent and its thread.</returns>
    public (AIAgent Agent, AgentThread Thread) CreateAgent(
        string instructions,
        ChatResponseFormat? responseFormat = null)
    {
        var options = new ChatClientAgentOptions(instructions);

        if (responseFormat != null)
        {
            options.ChatOptions = new ChatOptions
            {
                ResponseFormat = responseFormat
            };
        }

        var agent = new ChatClientAgent(_chatClient, options);
        var thread = agent.GetNewThread();

        return (agent, thread);
    }
}
