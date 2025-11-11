using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using WorkflowCustomAgentExecutorsSample.Models;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;
using AgentLmLocal;
using AgentLmLocal.Services;
using AgentLmLocal.Workflow;

namespace WorkflowCustomAgentExecutorsSample.Agents;

public sealed class PlannerAgent : InstrumentedAgent<string>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly LlmService _llmService;

    public PlannerAgent(
        string id,
        AgentFactory agentFactory,
        LlmService llmService)
        : base(id)
    {
        _llmService = llmService;

        var instructions = """
            You are an expert task planner that decomposes complex tasks into structured workflow plans.

            Your responsibilities:
            1. Analyze the given task and break it down into discrete steps
            2. Identify dependencies between steps
            3. Determine the type of each step (tool_call, llm_invocation, or conditional)
            4. Define input/output schemas for each step
            5. Generate an optimal execution order respecting dependencies
            6. Estimate the overall complexity (low, medium, high)

            Create comprehensive DAG-based plans that can be executed by other agents.
            Each node should have:
            - A unique ID
            - A type (tool_call, llm_invocation, conditional)
            - A clear description
            - Dependencies (list of node IDs that must complete first)
            - Input and output schemas

            Ensure the plan is executable, has no circular dependencies, and maximizes parallelism where possible.
            """;

        (_agent, _thread) = agentFactory.CreateAgent(
            instructions,
            ChatResponseFormat.ForJsonSchema<WorkflowPlan>());
    }

    protected override async ValueTask ExecuteInstrumentedAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            Task: {message}

            Please create a detailed workflow plan for this task. Break it down into specific steps
            with clear dependencies and execution order.
            """;

        var plan = await _llmService.InvokeStructuredAsync<WorkflowPlan>(
            _agent, _thread, prompt, cancellationToken);

        await context.AddEventAsync(new PlanGeneratedEvent(plan), cancellationToken);

        await context.SendMessageAsync(plan, cancellationToken: cancellationToken);
    }
}
