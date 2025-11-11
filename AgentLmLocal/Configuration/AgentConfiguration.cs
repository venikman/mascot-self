namespace AgentLmLocal.Configuration;

/// <summary>
/// Centralized configuration for the agent system.
/// </summary>
public sealed class AgentConfiguration
{
    /// <summary>
    /// LM Studio or OpenAI-compatible API endpoint.
    /// </summary>
    public string LmStudioEndpoint { get; init; } = "http://localhost:1234/v1";

    /// <summary>
    /// API key for authentication.
    /// </summary>
    public string ApiKey { get; init; } = "lm-studio";

    /// <summary>
    /// The model ID to use for LLM operations.
    /// </summary>
    public string ModelId { get; init; } = "openai/gpt-oss-20b";

    /// <summary>
    /// Minimum quality rating required for verification (1-10).
    /// </summary>
    public int MinimumRating { get; init; } = 7;

    /// <summary>
    /// Maximum number of refinement attempts.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Creates an AgentConfiguration instance from environment variables.
    /// </summary>
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
