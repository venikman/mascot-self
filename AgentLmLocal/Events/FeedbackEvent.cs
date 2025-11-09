// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json;
using AgentLmLocal.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentLmLocal.Events;

/// <summary>
/// A custom event to indicate that feedback has been provided.
/// </summary>
internal sealed class FeedbackEvent(FeedbackResult feedbackResult) : WorkflowEvent(feedbackResult)
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    public override string ToString() => $"Feedback:\n{JsonSerializer.Serialize(feedbackResult, this._options)}";
}
