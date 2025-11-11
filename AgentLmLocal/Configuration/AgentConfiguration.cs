namespace AgentLmLocal.Configuration;

public enum LlmProvider
{
    LmStudio,
    AzureOpenAI
}

public sealed class AgentConfiguration
{
    public LlmProvider Provider { get; init; } = LlmProvider.LmStudio;

    public string LmStudioEndpoint { get; init; } = "http://localhost:1234/v1";

    public string ApiKey { get; init; } = "lm-studio";

    public string ModelId { get; init; } = "openai/gpt-oss-20b";

    public string AzureOpenAIEndpoint { get; init; } = string.Empty;

    public string AzureOpenAIApiKey { get; init; } = string.Empty;

    public string AzureOpenAIDeployment { get; init; } = string.Empty;

    public string AzureOpenAIApiVersion { get; init; } = "2024-05-01-preview";

    public int MinimumRating { get; init; } = 7;

    public int MaxAttempts { get; init; } = 3;

    public static AgentConfiguration FromEnvironment()
    {
        var minimumRatingEnv = Environment.GetEnvironmentVariable("MINIMUM_RATING");
        var minimumRating = 7;
        if (int.TryParse(minimumRatingEnv, out var parsedRating))
        {
            minimumRating = Math.Clamp(parsedRating, 1, 10);
        }

        var maxAttemptsEnv = Environment.GetEnvironmentVariable("MAX_ATTEMPTS");
        var maxAttempts = 3;
        if (int.TryParse(maxAttemptsEnv, out var parsedAttempts))
        {
            maxAttempts = Math.Max(parsedAttempts, 1);
        }

        var providerEnv = Environment.GetEnvironmentVariable("LLM_PROVIDER");
        var provider = LlmProvider.LmStudio;
        if (!string.IsNullOrWhiteSpace(providerEnv) &&
            Enum.TryParse(providerEnv, ignoreCase: true, out LlmProvider parsedProvider))
        {
            provider = parsedProvider;
        }

        return new AgentConfiguration
        {
            Provider = provider,
            LmStudioEndpoint = Environment.GetEnvironmentVariable("LMSTUDIO_ENDPOINT") ?? "http://localhost:1234/v1",
            ApiKey = Environment.GetEnvironmentVariable("LMSTUDIO_API_KEY") ?? "lm-studio",
            ModelId = Environment.GetEnvironmentVariable("LMSTUDIO_MODEL") ?? "openai/gpt-oss-20b",
            AzureOpenAIEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? string.Empty,
            AzureOpenAIApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY") ?? string.Empty,
            AzureOpenAIDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? string.Empty,
            AzureOpenAIApiVersion = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION") ?? "2024-05-01-preview",
            MinimumRating = minimumRating,
            MaxAttempts = maxAttempts
        };
    }
}
