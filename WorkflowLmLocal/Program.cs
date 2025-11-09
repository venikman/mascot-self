using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Execution;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var lmStudioEndpoint = Environment.GetEnvironmentVariable("LM_STUDIO_BASE_URL") ?? "http://127.0.0.1:1234/v1/";
var lmStudioModelId = Environment.GetEnvironmentVariable("LM_STUDIO_MODEL_ID") ?? "lmstudio-community/Meta-Llama-3-8B-Instruct";
var lmStudioApiKey = Environment.GetEnvironmentVariable("LM_STUDIO_API_KEY") ?? "lm-studio";
var inputText = args.Length > 0 ? string.Join(" ", args) : "Hello world!";
var endpointUri = NormalizeEndpoint(lmStudioEndpoint);
var cancellationToken = CancellationToken.None;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
};

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddSimpleConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss ";
            options.SingleLine = true;
        })
        .SetMinimumLevel(LogLevel.Information);
});

var services = new ServiceCollection()
    .AddSingleton(loggerFactory)
    .BuildServiceProvider();

using var chatClient = new LmStudioChatClient(endpointUri, lmStudioModelId, lmStudioApiKey);

ChatClientAgent CreateTranslationAgent(string targetLanguage) =>
    new(
        chatClient,
        instructions: $"You are a translation assistant that only responds in {targetLanguage}. Translate the provided text to {targetLanguage} with no commentary.",
        loggerFactory: loggerFactory,
        services: services);

var frenchAgent = CreateTranslationAgent("French");
var spanishAgent = CreateTranslationAgent("Spanish");
var englishAgent = CreateTranslationAgent("English");

var workflow = new WorkflowBuilder(frenchAgent)
    .AddEdge(frenchAgent, spanishAgent)
    .AddEdge(spanishAgent, englishAgent)
    .Build();

Console.WriteLine("=== Agent workflow demo ===");
Console.WriteLine($"Input: {inputText}");

await RunWorkflowAsync(workflow, inputText, jsonOptions, cancellationToken);

static async Task RunWorkflowAsync(Workflow workflow, string input, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
{
    await using StreamingRun run = await InProcessExecution.StreamAsync(
        workflow,
        new ChatMessage(ChatRole.User, input),
        cancellationToken: cancellationToken);

    if (!await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false))
    {
        Console.WriteLine("Failed to enqueue the turn token, aborting.");
        return;
    }

    Console.WriteLine("\n=== Streaming updates ===");
    await foreach (WorkflowEvent evt in run.WatchStreamAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        if (evt is AgentRunUpdateEvent update)
        {
            var rendered = FormatEventData(update.Data, jsonOptions);
            Console.WriteLine($"{update.ExecutorId}: {rendered}");
        }
    }

    Console.WriteLine("\nWorkflow complete.");
}

static string FormatEventData(object? data, JsonSerializerOptions jsonOptions) => data switch
{
    ChatMessage message when !string.IsNullOrWhiteSpace(message.Text) => message.Text!,
    ChatMessage message when message.Text is null => "[empty assistant message]",
    string text => text,
    null => "(no data)",
    _ => JsonSerializer.Serialize(data, jsonOptions)
};

static Uri NormalizeEndpoint(string rawValue)
{
    if (!rawValue.EndsWith("/", StringComparison.Ordinal))
    {
        rawValue += "/";
    }

    if (!Uri.TryCreate(rawValue, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException($"LM Studio endpoint '{rawValue}' is not a valid absolute URI.");
    }

    return uri;
}

internal sealed class LmStudioChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string? _apiKey;

    public LmStudioChatClient(Uri baseUri, string modelId, string? apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = baseUri
        };
        _modelId = modelId;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken)
    {
        var payload = new ChatCompletionRequest(
            _modelId,
            ConvertMessages(messages),
            0.3);

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var completion = await JsonSerializer.DeserializeAsync<ChatCompletionResponse>(contentStream, _jsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("LM Studio returned an empty response.");

        var text = completion.Choices.FirstOrDefault()?.Message?.ReadContent();
        var assistantMessage = new ChatMessage(ChatRole.Assistant, text ?? "[no content returned]");
        return new ChatResponse(assistantMessage);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken) => throw new NotSupportedException("Streaming is not enabled for the LM Studio client.");

    public void Dispose() => _httpClient.Dispose();

    public object? GetService(Type serviceType, object? key) => null;

    private static IReadOnlyList<ChatCompletionMessage> ConvertMessages(IEnumerable<ChatMessage> messages)
    {
        return messages.Select(message => new ChatCompletionMessage(
            MapRole(message.Role),
            ExtractText(message)))
            .ToList();
    }

    private static string MapRole(ChatRole role)
    {
        if (role == ChatRole.Assistant)
        {
            return "assistant";
        }

        if (role == ChatRole.System)
        {
            return "system";
        }

        if (role == ChatRole.Tool)
        {
            return "tool";
        }

        return "user";
    }

    private static string ExtractText(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
        {
            return message.Text!;
        }

        if (message.Contents is null || message.Contents.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var content in message.Contents)
        {
            if (content is TextContent text)
            {
                builder.Append(text.Text);
            }
        }

        return builder.ToString();
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int? MaxTokens = null);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice> Choices);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionResponseMessage Message);

    private sealed record ChatCompletionResponseMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] JsonElement Content)
    {
        public string? ReadContent()
        {
            return Content.ValueKind switch
            {
                JsonValueKind.String => Content.GetString(),
                JsonValueKind.Array => ExtractArrayContent(Content),
                JsonValueKind.Object when Content.TryGetProperty("text", out var textElement) => textElement.GetString(),
                _ => Content.ToString()
            };
        }

        private static string? ExtractArrayContent(JsonElement arrayElement)
        {
            var builder = new StringBuilder();
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    builder.Append(item.GetString());
                }
                else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var text))
                {
                    builder.Append(text.GetString());
                }
            }

            return builder.Length > 0 ? builder.ToString() : null;
        }
    }
}
