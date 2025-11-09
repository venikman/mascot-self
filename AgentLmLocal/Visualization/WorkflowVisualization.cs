// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using System.Text;
using Microsoft.Agents.AI.Workflows;

namespace AgentLmLocal.Visualization;

/// <summary>
/// Represents a workflow visualization that can be exported to various formats.
/// </summary>
public class WorkflowVisualization
{
    private readonly Executor _startExecutor;
    private readonly List<(Executor from, Executor to)> _edges;

    public WorkflowVisualization(Executor startExecutor, List<(Executor from, Executor to)> edges)
    {
        _startExecutor = startExecutor;
        _edges = edges;
    }

    /// <summary>
    /// Saves the workflow as a DOT graph file.
    /// </summary>
    public string SaveDot(string filePath)
    {
        var dot = GenerateDot();
        File.WriteAllText(filePath, dot);
        return filePath;
    }

    /// <summary>
    /// Saves the workflow as a Mermaid diagram.
    /// </summary>
    public void SaveMermaid(string filePath)
    {
        var mermaid = GenerateMermaid();
        File.WriteAllText(filePath, mermaid);
    }

    /// <summary>
    /// Attempts to export the workflow visualization as an image using Graphviz.
    /// </summary>
    public bool TryExportImage(string dotPath, string outputPath, string format, out string? errorMessage)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dot",
                    Arguments = $"-T{format} \"{dotPath}\" -o \"{outputPath}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                errorMessage = process.StandardError.ReadToEnd();
                return false;
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private string GenerateDot()
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph Workflow {");
        sb.AppendLine("    rankdir=LR;");
        sb.AppendLine("    node [shape=box, style=rounded];");
        sb.AppendLine();

        // Add start node
        sb.AppendLine($"    start [label=\"Start\", shape=circle];");
        sb.AppendLine($"    start -> \"{_startExecutor.Id}\";");
        sb.AppendLine();

        // Add edges
        foreach (var (from, to) in _edges)
        {
            sb.AppendLine($"    \"{from.Id}\" -> \"{to.Id}\";");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private string GenerateMermaid()
    {
        var sb = new StringBuilder();
        sb.AppendLine("graph LR");
        sb.AppendLine($"    Start([Start]) --> {SanitizeMermaidId(_startExecutor.Id)}");

        // Add edges
        foreach (var (from, to) in _edges)
        {
            sb.AppendLine($"    {SanitizeMermaidId(from.Id)} --> {SanitizeMermaidId(to.Id)}");
        }

        return sb.ToString();
    }

    private static string SanitizeMermaidId(string id)
    {
        return id.Replace(" ", "_").Replace("-", "_");
    }
}
