// Copyright (c) Microsoft. All rights reserved.

using AgentLmLocal.Models;
using Microsoft.Agents.AI.Workflows;

namespace AgentLmLocal.Events;

/// <summary>
/// A custom event to indicate that a slogan has been generated.
/// </summary>
internal sealed class SloganGeneratedEvent(SloganResult sloganResult) : WorkflowEvent(sloganResult)
{
    public override string ToString() => $"Slogan: {sloganResult.Slogan}";
}
