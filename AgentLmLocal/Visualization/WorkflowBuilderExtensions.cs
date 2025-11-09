// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.Workflows;

namespace AgentLmLocal.Visualization;

/// <summary>
/// Extension methods for WorkflowBuilder to add visualization support.
/// </summary>
public static class WorkflowBuilderExtensions
{
    /// <summary>
    /// Adds an edge between two executors and records it for visualization.
    /// </summary>
    public static WorkflowBuilder AddVisualEdge(
        this WorkflowBuilder builder,
        WorkflowVisualizationRecorder recorder,
        Executor from,
        Executor to)
    {
        recorder.RecordEdge(from, to);
        return builder.AddEdge(from, to);
    }

    /// <summary>
    /// Sets the output executor and records it for visualization.
    /// </summary>
    public static WorkflowBuilder WithVisualOutputFrom(
        this WorkflowBuilder builder,
        WorkflowVisualizationRecorder recorder,
        Executor executor)
    {
        return builder.WithOutputFrom(executor);
    }
}
