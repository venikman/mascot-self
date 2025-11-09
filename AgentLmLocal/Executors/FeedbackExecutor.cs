// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Concurrent;
using System.Text.Json;
using AgentLmLocal.Events;
using AgentLmLocal.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace AgentLmLocal.Executors;

/// <summary>
/// A custom executor that uses an AI agent to provide feedback on a slogan.
/// </summary>
internal sealed class FeedbackExecutor : Executor<SloganResult>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly ConcurrentDictionary<IWorkflowContext, int> _attemptsByContext = new();

    public int MinimumRating { get; init; } = 8;

    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedbackExecutor"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the executor.</param>
    /// <param name="chatClient">The chat client to use for the AI agent.</param>
    public FeedbackExecutor(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new(instructions: "You are a professional editor. You will be given a slogan and the task it is meant to accomplish.")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<FeedbackResult>()
            }
        };

        this._agent = new ChatClientAgent(chatClient, agentOptions);
        this._thread = this._agent.GetNewThread();
    }

    public override async ValueTask HandleAsync(SloganResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var sloganMessage = $"""
            Here is a slogan for the task '{message.Task}':
            Slogan: {message.Slogan}
            Please provide feedback on this slogan, including comments, a rating from 1 to 10, and suggested actions for improvement.
            """;

        var response = await this._agent.RunAsync(sloganMessage, this._thread, cancellationToken: cancellationToken);
        var feedback = JsonSerializer.Deserialize<FeedbackResult>(response.Text)
            ?? throw new InvalidOperationException($"Failed to deserialize feedback. Response: {response.Text}");

        await context.AddEventAsync(new FeedbackEvent(feedback), cancellationToken);

        if (feedback.Rating >= this.MinimumRating)
        {
            await context.YieldOutputAsync($"The following slogan was accepted:\n\n{message.Slogan}", cancellationToken);
            return;
        }

        // Get current attempt count for this workflow context
        var attempts = _attemptsByContext.GetOrAdd(context, 0);

        if (attempts >= this.MaxAttempts)
        {
            await context.YieldOutputAsync($"The slogan was rejected after {this.MaxAttempts} attempts. Final slogan:\n\n{message.Slogan}", cancellationToken);
            return;
        }

        await context.SendMessageAsync(feedback, cancellationToken: cancellationToken);

        // Increment attempt count for this workflow context
        _attemptsByContext.AddOrUpdate(context, 1, (_, count) => count + 1);
    }
}
