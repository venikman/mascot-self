using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AgentLmLocal;

/// <summary>
/// Base class for instrumented agents that provides comprehensive telemetry integration.
/// Automatically tracks execution activities, errors, and performance metrics.
/// </summary>
/// <typeparam name="TInput">The input type for the agent.</typeparam>
public abstract class InstrumentedAgent<TInput> : Executor<TInput>
{
    protected static readonly ActivitySource ActivitySource = new("AgenticWorkflow");
    protected readonly ILogger Logger;
    protected readonly AgentInstrumentation Telemetry;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedAgent{TInput}"/> class.
    /// </summary>
    /// <param name="id">Unique identifier for the agent.</param>
    /// <param name="logger">Logger instance for the agent.</param>
    /// <param name="telemetry">Telemetry instrumentation instance.</param>
    protected InstrumentedAgent(string id, ILogger logger, AgentInstrumentation telemetry) : base(id)
    {
        Logger = logger;
        Telemetry = telemetry;
    }

    /// <summary>
    /// Handles incoming messages with comprehensive telemetry tracking.
    /// </summary>
    /// <param name="message">The input message to process.</param>
    /// <param name="context">The workflow context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public override async ValueTask HandleAsync(TInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity($"Agent.{Id}.Handle");
        
        activity?.SetTag("agent.id", Id);
        activity?.SetTag("agent.message.type", typeof(TInput).Name);
        
        var startTime = DateTime.UtcNow;
        Telemetry.RecordActivity(Id, "started");

        await ExecuteInstrumentedAsync(message, context, cancellationToken);
        
        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        Telemetry.RecordActivity(Id, "completed", executionTime);
        
        activity?.SetStatus(ActivityStatusCode.Ok);
        activity?.SetTag("agent.execution.time", executionTime);
    }

    /// <summary>
    /// Executes the agent's core logic with telemetry instrumentation.
    /// Must be implemented by derived classes.
    /// </summary>
    /// <param name="message">The input message to process.</param>
    /// <param name="context">The workflow context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected abstract ValueTask ExecuteInstrumentedAsync(TInput message, IWorkflowContext context, CancellationToken cancellationToken);
}