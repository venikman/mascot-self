using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AgentLmLocal;

/// <summary>
/// Provides comprehensive telemetry instrumentation for the agentic workflow system.
/// Tracks activities, errors, execution times, and active agent counts using OpenTelemetry.
/// </summary>
public sealed class AgentInstrumentation : IDisposable
{
    public static readonly ActivitySource ActivitySource = new("AgenticWorkflow");
    public static readonly Meter Meter = new("AgenticWorkflow");
    
    // Core metrics
    public readonly Counter<long> ActivitiesCounter;
    public readonly Counter<long> ErrorsCounter;
    public readonly Histogram<double> ExecutionTime;
    
    private readonly List<IDisposable> _disposables = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentInstrumentation"/> class.
    /// Sets up all telemetry metrics and instruments.
    /// </summary>
    public AgentInstrumentation()
    {
        ActivitiesCounter = Meter.CreateCounter<long>(
            "agent.activities.total",
            description: "Total number of agent activities");
        
        ErrorsCounter = Meter.CreateCounter<long>(
            "agent.errors.total",
            description: "Total number of agent errors");
        
        ExecutionTime = Meter.CreateHistogram<double>(
            "agent.execution.duration",
            unit: "ms",
            description: "Agent execution time in milliseconds");
        
        // Keep metrics minimal; drop active agents gauge for simplicity
    }

    /// <summary>
    /// Records an agent activity with optional duration tracking.
    /// </summary>
    /// <param name="agentId">Unique identifier of the agent.</param>
    /// <param name="activityType">Type of activity being performed.</param>
    /// <param name="duration">Optional execution duration in milliseconds.</param>
    public void RecordActivity(string agentId, string activityType, double? duration = null)
    {
        var tags = new TagList
        {
            { "agent.id", agentId },
            { "activity.type", activityType }
        };
        
        ActivitiesCounter.Add(1, tags);
        
        if (duration.HasValue)
        {
            ExecutionTime.Record(duration.Value, tags);
        }
    }

    /// <summary>
    /// Records an agent error with error type classification.
    /// </summary>
    /// <param name="agentId">Unique identifier of the agent.</param>
    /// <param name="errorType">Type or category of the error.</param>
    public void RecordError(string agentId, string errorType)
    {
        ErrorsCounter.Add(1, new TagList
        {
            { "agent.id", agentId },
            { "error.type", errorType }
        });
    }

    // Removed active agents tracking to keep telemetry minimal

    /// <summary>
    /// Disposes all telemetry resources and instruments.
    /// </summary>
    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
        
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }
}