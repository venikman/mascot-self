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
    /// The OTLP endpoint for telemetry export.
    /// </summary>
    public string OtlpEndpoint { get; init; } = "http://localhost:19149";

    /// <summary>
    /// Trace sampling ratio (0.0 to 1.0).
    /// </summary>
    public double TraceSamplingRatio { get; init; } = 1.0;

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
        var samplingRatioEnv = Environment.GetEnvironmentVariable("OTEL_TRACE_SAMPLING_RATIO");
        var samplingRatio = 1.0;
        if (double.TryParse(samplingRatioEnv, out var parsedRatio))
        {
            samplingRatio = Math.Clamp(parsedRatio, 0.0, 1.0);
        }

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
            OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                          ?? Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL")
                          ?? "http://localhost:19149",
            TraceSamplingRatio = samplingRatio,
            MinimumRating = minimumRating,
            MaxAttempts = maxAttempts
        };
    }
}
