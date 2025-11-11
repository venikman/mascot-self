using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;

namespace AgentLmLocal;

/// <summary>
/// Base class for agents.
/// </summary>
/// <typeparam name="TInput">The input type for the agent.</typeparam>
public abstract class InstrumentedAgent<TInput> : Executor<TInput>
{
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstrumentedAgent{TInput}"/> class.
    /// </summary>
    /// <param name="id">Unique identifier for the agent.</param>
    /// <param name="logger">Logger instance for the agent.</param>
    protected InstrumentedAgent(string id, ILogger logger) : base(id)
    {
        Logger = logger;
    }

    /// <summary>
    /// Handles incoming messages.
    /// </summary>
    /// <param name="message">The input message to process.</param>
    /// <param name="context">The workflow context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public override async ValueTask HandleAsync(TInput message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        await ExecuteInstrumentedAsync(message, context, cancellationToken);
    }

    /// <summary>
    /// Executes the agent's core logic.
    /// Must be implemented by derived classes.
    /// </summary>
    /// <param name="message">The input message to process.</param>
    /// <param name="context">The workflow context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected abstract ValueTask ExecuteInstrumentedAsync(TInput message, IWorkflowContext context, CancellationToken cancellationToken);
}