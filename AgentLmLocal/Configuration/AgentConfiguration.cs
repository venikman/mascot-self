namespace AgentLmLocal.Configuration;

public sealed class AgentConfiguration
{
    public string OpenAIApiKey { get; init; } = string.Empty;

    public string OpenAIModelId { get; init; } = "kat-dev-mlx";

    public string OpenAIBaseUrl { get; init; } = "http://127.0.0.1:1234/v1";

    public string OpenAIVersion { get; init; } = string.Empty;

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

        return new AgentConfiguration
        {
            OpenAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty,
            OpenAIModelId = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "gpt-4o-mini",
            OpenAIBaseUrl = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com/v1",
            OpenAIVersion = Environment.GetEnvironmentVariable("OPENAI_VERSION") ?? string.Empty,
            MinimumRating = minimumRating,
            MaxAttempts = maxAttempts
        };
    }
}
