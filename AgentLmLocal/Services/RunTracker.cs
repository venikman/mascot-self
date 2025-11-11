using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AgentLmLocal.Services;

public sealed class RunTracker
{
    private readonly ConcurrentDictionary<string, RunRecord> _runs = new(StringComparer.Ordinal);

    public void Register(string workflowId, string task)
    {
        _runs[workflowId] = new RunRecord(workflowId, task);
    }

    public void RecordEvent(string workflowId, string eventName)
    {
        if (_runs.TryGetValue(workflowId, out var record))
        {
            record.RecordEvent(eventName);
        }
    }

    public void AppendOutput(string workflowId, string output)
    {
        if (_runs.TryGetValue(workflowId, out var record))
        {
            record.AddOutput(output);
        }
    }

    public void MarkCompleted(string workflowId)
    {
        if (_runs.TryGetValue(workflowId, out var record))
        {
            record.MarkCompleted();
        }
    }

    public void MarkFailed(string workflowId, Exception exception)
    {
        if (_runs.TryGetValue(workflowId, out var record))
        {
            record.MarkFailed(exception);
        }
    }

    public bool TryGetStatus(string workflowId, out RunStatusSnapshot snapshot)
    {
        if (_runs.TryGetValue(workflowId, out var record))
        {
            snapshot = record.CreateSnapshot();
            return true;
        }

        snapshot = default!;
        return false;
    }

    private sealed class RunRecord
    {
        private readonly object _syncRoot = new();
        private readonly ConcurrentDictionary<string, int> _eventCounts = new(StringComparer.Ordinal);
        private readonly List<string> _outputs = new();

        public RunRecord(string workflowId, string task)
        {
            WorkflowId = workflowId;
            Task = task;
            StartedAt = DateTimeOffset.UtcNow;
            Status = "running";
        }

        public string WorkflowId { get; }
        public string Task { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? CompletedAt { get; private set; }
        public string Status { get; private set; }
        public string? Error { get; private set; }

        public void RecordEvent(string eventName) =>
            _eventCounts.AddOrUpdate(eventName, 1, static (_, current) => current + 1);

        public void AddOutput(string output)
        {
            lock (_syncRoot)
            {
                _outputs.Add(output);
            }
        }

        public void MarkCompleted()
        {
            Status = "completed";
            CompletedAt = DateTimeOffset.UtcNow;
        }

        public void MarkFailed(Exception exception)
        {
            Status = "failed";
            CompletedAt = DateTimeOffset.UtcNow;
            Error = exception.Message;
        }

        public RunStatusSnapshot CreateSnapshot()
        {
            lock (_syncRoot)
            {
                return new RunStatusSnapshot(
                    WorkflowId,
                    Task,
                    Status,
                    StartedAt,
                    CompletedAt,
                    new Dictionary<string, int>(_eventCounts, StringComparer.Ordinal),
                    _outputs.ToArray(),
                    Error);
            }
        }
    }
}

public sealed record RunStatusSnapshot(
    string WorkflowId,
    string Task,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyDictionary<string, int> Events,
    IReadOnlyList<string> Outputs,
    string? Error);
