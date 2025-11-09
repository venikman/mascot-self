// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace AgentLmLocal.Models;

/// <summary>
/// A class representing the output of the slogan writer agent.
/// </summary>
public sealed class SloganResult
{
    [JsonPropertyName("task")]
    public required string Task { get; set; }

    [JsonPropertyName("slogan")]
    public required string Slogan { get; set; }
}
