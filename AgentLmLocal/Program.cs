using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowCustomAgentExecutorsSample;
using AgentLmLocal.Configuration;
using AgentLmLocal.Services;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System;

namespace AgentLmLocal;

public static class Program
{
    private static Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var config = AgentConfiguration.FromEnvironment();
        builder.Services.AddSingleton(config);

        builder.Services.AddSingleton<IChatClient>(sp =>
        {
            var lmStudioClient = new OpenAI.OpenAIClient(
                new ApiKeyCredential(config.ApiKey),
                new OpenAI.OpenAIClientOptions
                {
                    Endpoint = new Uri(config.LmStudioEndpoint)
                });

            return lmStudioClient.GetChatClient(config.ModelId).AsIChatClient();
        });

        builder.Services.AddSingleton<AgentFactory>();
        builder.Services.AddSingleton<LlmService>();
        builder.Services.AddSingleton<AgenticWorkflowExample>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.MapPost("/run", (AgenticWorkflowExample example, RunRequest request) =>
        {
            var workflowId = Guid.NewGuid().ToString("N");

            _ = Task.Run(() => example.RunWorkflowAsync(request.Task));

            return Results.Accepted($"/runs/{workflowId}", new { workflowId, status = "started" });
        });

        return app.RunAsync();
    }

    private sealed record RunRequest(string Task);
}
