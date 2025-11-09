// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AgentLmLocal.Models;

/// <summary>
/// A class representing the output of the feedback agent.
/// </summary>
public sealed class FeedbackResult
{
    [JsonPropertyName("comments")]
    public string Comments { get; set; } = string.Empty;

    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("actions")]
    public string Actions { get; set; } = string.Empty;
}
