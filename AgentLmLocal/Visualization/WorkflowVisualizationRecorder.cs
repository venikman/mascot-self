// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.Workflows;

namespace AgentLmLocal.Visualization;

/// <summary>
/// Records workflow execution for visualization purposes.
/// </summary>
public class WorkflowVisualizationRecorder
{
    private readonly Executor _startExecutor;
    private readonly List<(Executor from, Executor to)> _edges = new();

    public WorkflowVisualizationRecorder(Executor startExecutor)
    {
        _startExecutor = startExecutor;
    }

    /// <summary>
    /// Records an edge between two executors for visualization.
    /// </summary>
    public void RecordEdge(Executor from, Executor to)
    {
        _edges.Add((from, to));
    }

    /// <summary>
    /// Creates a visualization of the recorded workflow.
    /// </summary>
    public WorkflowVisualization CreateVisualization()
    {
        return new WorkflowVisualization(_startExecutor, _edges);
    }
}
