using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
};
jsonOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(new DefaultJsonTypeInfoResolver());

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.None);
});

var services = new ServiceCollection()
    .AddSingleton(loggerFactory)
    .BuildServiceProvider();

var weatherTool = AIFunctionFactory.Create(
    (WeatherQuery query) => WeatherService.Lookup(query),
    name: WeatherService.ToolName,
    description: "Looks up a fake forecast so the middleware demo runs offline.",
    serializerOptions: jsonOptions);

var baseClient = new DemoChatClient(jsonOptions);

var chatClientWithMiddleware = baseClient
    .AsBuilder()
    .Use(getResponseFunc: ChatClientLoggerMiddleware, getStreamingResponseFunc: null)
    .Build();

var functionAwareClient = new FunctionInvokingChatClient(chatClientWithMiddleware, loggerFactory, services);

var agent = new ChatClientAgent(
    functionAwareClient,
    instructions: "You are a weather-savvy travel planner. Always invoke tools before guessing.",
    tools: new List<AITool> { weatherTool },
    loggerFactory: loggerFactory,
    services: services);

var middlewareAgent = agent
    .AsBuilder()
    .Use(AgentRunLoggerMiddleware, runStreamingFunc: null)
    .Use(FunctionInvocationLoggerMiddleware)
    .Build();

var thread = middlewareAgent.GetNewThread();
var requestChatOptions = new ChatOptions
{
    Tools = new List<AITool> { weatherTool }
};

var response = await middlewareAgent.RunAsync(
    "I'm flying to Tokyo tomorrow. Should I pack an umbrella for Tokyo?",
    thread,
    new ChatClientAgentRunOptions(requestChatOptions),
    CancellationToken.None);

var finalText = response.Messages.LastOrDefault(static m => m.Role == ChatRole.Assistant)?.Text?.Trim();
Console.WriteLine(finalText ?? "No assistant response was produced.");

async Task<AgentRunResponse> AgentRunLoggerMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentThread? thread,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    return await innerAgent
        .RunAsync(messages, thread, options, cancellationToken)
        .ConfigureAwait(false);
}

async ValueTask<object?> FunctionInvocationLoggerMiddleware(
    AIAgent currentAgent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    if (context.Arguments is IDictionary<string, object?> dict)
    {
        var queryArgs = ExtractQueryArgs(dict);
        if (!queryArgs.TryGetValue("location", out var value) || string.IsNullOrWhiteSpace(Convert.ToString(value)))
        {
            queryArgs["location"] = "Seattle";
        }
    }

    return await next(context, cancellationToken).ConfigureAwait(false);
}

IDictionary<string, object?> ExtractQueryArgs(IDictionary<string, object?> args)
{
    if (!args.TryGetValue("query", out var queryValue) || queryValue is null)
    {
        var created = new Dictionary<string, object?>();
        args["query"] = created;
        return created;
    }

    if (queryValue is IDictionary<string, object?> typed)
    {
        return typed;
    }

    if (queryValue is JsonElement json && json.ValueKind == JsonValueKind.Object)
    {
        var hydrated = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, jsonOptions)
            ?? new Dictionary<string, object?>();
        args["query"] = hydrated;
        return hydrated;
    }

    var fallback = new Dictionary<string, object?>
    {
        ["value"] = queryValue
    };
    args["query"] = fallback;
    return fallback;
}

async Task<ChatResponse> ChatClientLoggerMiddleware(
    IEnumerable<ChatMessage> messages,
    ChatOptions? options,
    IChatClient innerClient,
    CancellationToken cancellationToken)
{
    return await innerClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
}

internal sealed class DemoChatClient : IChatClient
{
    private readonly JsonSerializerOptions _jsonOptions;

    public DemoChatClient(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
    }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        var transcript = messages.ToList();
        var needsToolCall = transcript.All(m => m.Role != ChatRole.Tool);

        if (needsToolCall)
        {
            var user = transcript.Last(m => m.Role == ChatRole.User);
            var location = WeatherService.ExtractLocation(user.Text) ?? "Seattle";
            var call = new FunctionCallContent(
                Guid.NewGuid().ToString("N"),
                WeatherService.ToolName,
                new Dictionary<string, object?>
                {
                    ["query"] = new Dictionary<string, object?>
                    {
                        ["location"] = location
                    }
                });

            var assistant = new ChatMessage(ChatRole.Assistant, new List<AIContent> { call });
            return Task.FromResult(new ChatResponse(assistant));
        }

        var toolMessage = transcript.Last(m => m.Role == ChatRole.Tool);
        var resultContent = toolMessage.Contents?
            .OfType<FunctionResultContent>()
            .FirstOrDefault();

        WeatherReport? report = resultContent?.Result switch
        {
            WeatherReport typed => typed,
            JsonElement json => json.Deserialize<WeatherReport>(_jsonOptions),
            null => null,
            _ => JsonSerializer.Deserialize<WeatherReport>(JsonSerializer.Serialize(resultContent.Result, _jsonOptions), _jsonOptions)
        };

        var text = report is null
            ? "I could not read the tool output."
            : WeatherService.FormatResponse(report);

        var finalAssistant = new ChatMessage(ChatRole.Assistant, text);
        return Task.FromResult(new ChatResponse(finalAssistant));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken) => throw new NotSupportedException("Streaming is not implemented in the local demo client.");

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? key) => null;
}

internal static class WeatherService
{
    public const string ToolName = "get_weather";

    private static readonly IReadOnlyDictionary<string, WeatherReport> Samples = new Dictionary<string, WeatherReport>(StringComparer.OrdinalIgnoreCase)
    {
        ["Tokyo"] = new("Tokyo", "Expect steady rain with high humidity.", 68),
        ["Seattle"] = new("Seattle", "Drizzle most of the day; skies clear overnight.", 64),
        ["Lisbon"] = new("Lisbon", "Sunny with a light Atlantic breeze.", 78)
    };

    public static WeatherReport Lookup(WeatherQuery query)
    {
        var location = string.IsNullOrWhiteSpace(query.Location) ? "Seattle" : query.Location.Trim();
        return Samples.TryGetValue(location, out var report)
            ? report
            : new WeatherReport(location, "Mild conditions, no precipitation expected.", 72);
    }

    public static string FormatResponse(WeatherReport report) =>
        $"Forecast for {report.Location}: {report.Summary} High near {report.HighFahrenheit}\u00B0F.";

    public static string? ExtractLocation(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var tokens = content.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens.Reverse())
        {
            var trimmed = token.Trim().TrimEnd('?', '!', '.', ',');
            if (trimmed.Length >= 3 && char.IsLetter(trimmed[0]) && char.IsUpper(trimmed[0]))
            {
                return trimmed;
            }
        }

        return null;
    }
}

internal sealed record WeatherQuery(string? Location);

internal sealed record WeatherReport(string Location, string Summary, int HighFahrenheit);
