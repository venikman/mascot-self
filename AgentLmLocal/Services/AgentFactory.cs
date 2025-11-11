using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace AgentLmLocal.Services;

public sealed class AgentFactory
{
    private readonly IChatClient _chatClient;

    public AgentFactory(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

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
