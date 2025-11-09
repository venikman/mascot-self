// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AgentLmLocal.Events;
using AgentLmLocal.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace AgentLmLocal.Executors;

/// <summary>
/// A custom executor that uses an AI agent to generate slogans based on a given task.
/// Note that this executor has two message handlers:
/// 1. HandleAsync(string message): Handles the initial task to create a slogan.
/// 2. HandleAsync(Feedback message): Handles feedback to improve the slogan.
/// </summary>
internal sealed class SloganWriterExecutor : Executor
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    /// <summary>
    /// Initializes a new instance of the <see cref="SloganWriterExecutor"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the executor.</param>
    /// <param name="chatClient">The chat client to use for the AI agent.</param>
    public SloganWriterExecutor(string id, IChatClient chatClient) : base(id)
    {
        ChatClientAgentOptions agentOptions = new(instructions: "You are a professional slogan writer. You will be given a task to create a slogan.")
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<SloganResult>()
            }
        };

        this._agent = new ChatClientAgent(chatClient, agentOptions);
        this._thread = this._agent.GetNewThread();
    }

    protected override RouteBuilder ConfigureRoutes(RouteBuilder routeBuilder) =>
        routeBuilder.AddHandler<string, SloganResult>(this.HandleAsync)
                    .AddHandler<FeedbackResult, SloganResult>(this.HandleAsync);

    public async ValueTask<SloganResult> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var result = await this._agent.RunAsync(message, this._thread, cancellationToken: cancellationToken);

        var sloganResult = JsonSerializer.Deserialize<SloganResult>(result.Text)
            ?? throw new InvalidOperationException($"Failed to deserialize slogan result. Response: {result.Text}");

        await context.AddEventAsync(new SloganGeneratedEvent(sloganResult), cancellationToken);
        return sloganResult;
    }

    public async ValueTask<SloganResult> HandleAsync(FeedbackResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var feedbackMessage = $"""
            Here is the feedback on your previous slogan:
            Comments: {message.Comments}
            Rating: {message.Rating}
            Suggested Actions: {message.Actions}

            Please use this feedback to improve your slogan.
            """;

        var result = await this._agent.RunAsync(feedbackMessage, this._thread, cancellationToken: cancellationToken);
        var sloganResult = JsonSerializer.Deserialize<SloganResult>(result.Text)
            ?? throw new InvalidOperationException($"Failed to deserialize slogan result. Response: {result.Text}");

        await context.AddEventAsync(new SloganGeneratedEvent(sloganResult), cancellationToken);
        return sloganResult;
    }
}
