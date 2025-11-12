using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace AgentLmLocal;

public abstract class InstrumentedAgent<TInput> : Executor<TInput>
{
    protected InstrumentedAgent(string id) : base(id)
    {
    }

    public override ValueTask HandleAsync(
        TInput message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        ExecuteInstrumentedAsync(message, context, cancellationToken);

    protected abstract ValueTask ExecuteInstrumentedAsync(TInput message, IWorkflowContext context, CancellationToken cancellationToken);
}