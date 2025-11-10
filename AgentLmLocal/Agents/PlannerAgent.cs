using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using WorkflowCustomAgentExecutorsSample.Models;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;
using AgentLmLocal;

namespace WorkflowCustomAgentExecutorsSample.Agents;

/// <summary>
/// PlannerAgent: Decomposes complex tasks into multi-step DAGs (Directed Acyclic Graphs).
///
/// This agent analyzes incoming tasks and generates structured plans with typed nodes
/// representing tool calls, LLM invocations, and conditional branches. It manages
/// dependencies between steps and produces an execution order.
///
/// Based on the agentic workflows pattern described in the research paper.
/// </summary>
public sealed class PlannerAgent : InstrumentedAgent<string>
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlannerAgent"/> class.
    /// </summary>
    /// <param name="id">A unique identifier for the planner agent.</param>
    /// <param name="chatClient">The chat client to use for the AI agent.</param>
    /// <param name="logger">Logger instance for telemetry.</param>
    /// <param name="telemetry">Telemetry instrumentation instance.</param>
    public PlannerAgent(string id, IChatClient chatClient, ILogger<PlannerAgent> logger, AgentInstrumentation telemetry) 
        : base(id, logger, telemetry)
    {
        ChatClientAgentOptions agentOptions = new(
            instructions: """
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
            """)
        {
            ChatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema<WorkflowPlan>()
            }
        };

        this._agent = new ChatClientAgent(chatClient, agentOptions);
        this._thread = this._agent.GetNewThread();
    }

    /// <summary>
    /// Handles incoming task requests and generates workflow plans with telemetry instrumentation.
    /// </summary>
    protected override async ValueTask ExecuteInstrumentedAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Agent.Planner.PlanGeneration");
        
        activity?.SetTag("task.content", message);
        activity?.SetTag("task.length", message.Length);
        
        Telemetry.RecordActivity(Id, "planning_started");
        
        
        {
            var prompt = $"""
                Task: {message}

                Please create a detailed workflow plan for this task. Break it down into specific steps
                with clear dependencies and execution order.
                """;

            var result = await this._agent.RunAsync(prompt, this._thread, cancellationToken: cancellationToken);

            var plan = JsonSerializer.Deserialize<WorkflowPlan>(result.Text)!;

            activity?.SetTag("plan.nodes.count", plan.Nodes.Count);
            activity?.SetTag("plan.complexity", plan.EstimatedComplexity);
            
            Telemetry.RecordActivity(Id, "planning_completed");

            // Emit event for monitoring
            await context.AddEventAsync(new PlanGeneratedEvent(plan), cancellationToken);

            // Send the plan to the next executor
            await context.SendMessageAsync(plan, cancellationToken: cancellationToken);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
    }
}

/// <summary>
/// Event emitted when a workflow plan is generated.
/// </summary>
public sealed class PlanGeneratedEvent(WorkflowPlan plan) : WorkflowEvent(plan)
{
    public override string ToString()
    {
        var nodeCount = plan.Nodes.Count;
        var complexity = plan.EstimatedComplexity;
        return $"Plan Generated: {nodeCount} nodes, complexity: {complexity}\nTask: {plan.Task}";
    }
}
