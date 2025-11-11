namespace AgentLmLocal.Configuration;

public sealed class AgentConfiguration
{
    public string LmStudioEndpoint { get; init; } = "http://localhost:1234/v1";

    public string ApiKey { get; init; } = "lm-studio";

    public string ModelId { get; init; } = "openai/gpt-oss-20b";

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
            LmStudioEndpoint = Environment.GetEnvironmentVariable("LMSTUDIO_ENDPOINT") ?? "http://localhost:1234/v1",
            ApiKey = Environment.GetEnvironmentVariable("LMSTUDIO_API_KEY") ?? "lm-studio",
            ModelId = Environment.GetEnvironmentVariable("LMSTUDIO_MODEL") ?? "openai/gpt-oss-20b",
            MinimumRating = minimumRating,
            MaxAttempts = maxAttempts
        };
    }
}
