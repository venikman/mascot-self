using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace AgentHello.AI;

public record ChatRequest(string? Text);

public class AIOptions
{
    public const string SectionName = "AI";

    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? Endpoint { get; set; }
}

public interface IAiRequestOrchestrator
{
    Task<IResult> HandleAsync(ChatRequest request, HttpContext httpContext, CancellationToken cancellationToken);
}

public sealed class AiRequestOrchestrator : IAiRequestOrchestrator
{
    private readonly IChatClient _chatClient;
    private readonly IOptionsSnapshot<AIOptions> _options;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<AiRequestOrchestrator> _logger;

    public AiRequestOrchestrator(
        IChatClient chatClient,
        IOptionsSnapshot<AIOptions> options,
        ActivitySource activitySource,
        ILogger<AiRequestOrchestrator> logger)
    {
        _chatClient = chatClient;
        _options = options;
        _activitySource = activitySource;
        _logger = logger;
    }

    public async Task<IResult> HandleAsync(ChatRequest request, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest("Request body is required");
        }

        var cfg = _options.Value;
        var prompt = request.Text ?? string.Empty;
        using var aiAct = _activitySource.StartActivity("ai.chat", ActivityKind.Internal);
        if (aiAct != null)
        {
            aiAct.SetTag("ai.provider", "openai");
            aiAct.SetTag("ai.model", ResolveModel(cfg));
            aiAct.SetTag("ai.endpoint", ResolveEndpoint(cfg));
            aiAct.SetTag("ai.input.length", prompt.Length);
            aiAct.SetTag("ai.tools.count", 1);
            aiAct.SetTag("ai.tool.names", "GetTime");
        }

        _logger.LogInformation(
            "ai.chat.start {ai_provider} {ai_model} {ai_endpoint} {ai_input_length} {TraceId} {SpanId} {ParentSpanId} {RequestPath} {RequestMethod} {Host} {Scheme}",
            "openai",
            ResolveModel(cfg),
            ResolveEndpoint(cfg),
            prompt.Length,
            aiAct?.TraceId.ToString(),
            aiAct?.SpanId.ToString(),
            aiAct?.ParentSpanId.ToString(),
            httpContext.Request.Path,
            httpContext.Request.Method,
            httpContext.Request.Host.ToString(),
            httpContext.Request.Scheme);

        using var prepAct = _activitySource.StartActivity("ai.prepare", ActivityKind.Internal);
        _logger.LogInformation(
            "ai.prepare.start {TraceId} {SpanId} {ParentSpanId} {RequestPath} {RequestMethod} {Host} {Scheme}",
            prepAct?.TraceId.ToString(),
            prepAct?.SpanId.ToString(),
            prepAct?.ParentSpanId.ToString(),
            httpContext.Request.Path,
            httpContext.Request.Method,
            httpContext.Request.Host.ToString(),
            httpContext.Request.Scheme);

        prepAct?.SetTag("ai.prompt.length", prompt.Length);
        prepAct?.SetTag("ai.tools.count", 1);

        var getTime = () => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var chatOptions = new ChatOptions { Tools = [AIFunctionFactory.Create(getTime)] };

        if (_chatClient == null)
        {
            return Results.Problem("AI client unavailable", statusCode: 503);
        }

        var endpointUri = new Uri(ResolveEndpoint(cfg));
        using var invokeAct = _activitySource.StartActivity("ai.invoke", ActivityKind.Internal);
        invokeAct?.SetTag("net.peer.name", endpointUri.Host);
        invokeAct?.SetTag("http.method", "POST");
        invokeAct?.SetTag("http.target", endpointUri.AbsolutePath);

        _logger.LogInformation(
            "ai.invoke.start {TraceId} {SpanId} {ParentSpanId} {net_peer_name} {http_target} {RequestPath} {RequestMethod} {Host} {Scheme}",
            invokeAct?.TraceId.ToString(),
            invokeAct?.SpanId.ToString(),
            invokeAct?.ParentSpanId.ToString(),
            endpointUri.Host,
            endpointUri.AbsolutePath,
            httpContext.Request.Path,
            httpContext.Request.Method,
            httpContext.Request.Host.ToString(),
            httpContext.Request.Scheme);

        try
        {
            var response = await _chatClient.GetResponseAsync(prompt, chatOptions, cancellationToken);
            var responseText = response?.ToString() ?? string.Empty;
            aiAct?.SetTag("ai.output.length", responseText.Length);
            aiAct?.SetTag("ai.status_code", "ok");
            invokeAct?.SetTag("ai.status_code", "ok");
            invokeAct?.SetTag("ai.output.length", responseText.Length);

            _logger.LogInformation(
                "ai.invoke.end {ai_status_code} {ai_output_length} {TraceId} {SpanId} {ParentSpanId} {RequestPath} {RequestMethod} {Host} {Scheme}",
                "ok",
                responseText.Length,
                invokeAct?.TraceId.ToString(),
                invokeAct?.SpanId.ToString(),
                invokeAct?.ParentSpanId.ToString(),
                httpContext.Request.Path,
                httpContext.Request.Method,
                httpContext.Request.Host.ToString(),
                httpContext.Request.Scheme);

            _logger.LogInformation(
                "ai.chat.end {ai_status_code} {ai_output_length} {TraceId} {SpanId} {ParentSpanId} {RequestPath} {RequestMethod} {Host} {Scheme}",
                "ok",
                responseText.Length,
                aiAct?.TraceId.ToString(),
                aiAct?.SpanId.ToString(),
                aiAct?.ParentSpanId.ToString(),
                httpContext.Request.Path,
                httpContext.Request.Method,
                httpContext.Request.Host.ToString(),
                httpContext.Request.Scheme);

            return Results.Json(new { response = responseText });
        }
        catch
        {
            aiAct?.SetTag("ai.status_code", "error");
            aiAct?.SetTag("error", true);
            invokeAct?.SetTag("ai.status_code", "error");

            _logger.LogWarning(
                "ai.chat.end {ai_status_code} {TraceId} {SpanId} {ParentSpanId} {RequestPath} {RequestMethod} {Host} {Scheme}",
                "error",
                aiAct?.TraceId.ToString(),
                aiAct?.SpanId.ToString(),
                aiAct?.ParentSpanId.ToString(),
                httpContext.Request.Path,
                httpContext.Request.Method,
                httpContext.Request.Host.ToString(),
                httpContext.Request.Scheme);

            _logger.LogWarning(
                "ai.invoke.end {ai_status_code} {TraceId} {SpanId} {ParentSpanId} {RequestPath} {RequestMethod} {Host} {Scheme}",
                "error",
                invokeAct?.TraceId.ToString(),
                invokeAct?.SpanId.ToString(),
                invokeAct?.ParentSpanId.ToString(),
                httpContext.Request.Path,
                httpContext.Request.Method,
                httpContext.Request.Host.ToString(),
                httpContext.Request.Scheme);

            var now = DateTime.Now.ToString("O");
            return Results.Ok(new { response = $"Agent: Hello, agent world!\nTool: time => {now}" });
        }
    }

    private static string ResolveModel(AIOptions options) => options.Model ?? Environment.GetEnvironmentVariable("AI_MODEL") ?? "kat-dev-mlx";
    private static string ResolveEndpoint(AIOptions options) => options.Endpoint ?? Environment.GetEnvironmentVariable("AI_BASE_URL") ?? "http://localhost:1234/v1";
}

public static class AgentHelloServiceCollectionExtensions
{
    public static IServiceCollection AddAgentHelloServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AIOptions>().Bind(configuration.GetSection(AIOptions.SectionName));
        services.AddSingleton(new ActivitySource("AgentHello.AI"));

        services.AddSingleton<IChatClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptionsMonitor<AIOptions>>().CurrentValue;
            var model = cfg.Model ?? Environment.GetEnvironmentVariable("AI_MODEL") ?? "kat-dev-mlx";
            var apiKey = cfg.ApiKey ?? Environment.GetEnvironmentVariable("AI_API_KEY") ?? "lm-key";

            var http = new HttpClient();
            http.DefaultRequestHeaders.TryAddWithoutValidation("version", "2");
            var options = new OpenAIClientOptions
            {
                Transport = new HttpClientPipelineTransport(http)
            };
            var endpoint = cfg.Endpoint ?? Environment.GetEnvironmentVariable("AI_BASE_URL") ?? "http://localhost:1234/v1";
            options.Endpoint = new Uri(endpoint);
            var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
            var chat = client.GetChatClient(model);
            return new ChatClientBuilder(chat.AsIChatClient()).UseFunctionInvocation().Build();
        });

        services.AddSingleton<IAiRequestOrchestrator, AiRequestOrchestrator>();

        return services;
    }
}
