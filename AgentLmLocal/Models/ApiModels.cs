namespace AgentLmLocal.Models;

/// <summary>
/// Request model for the /run endpoint
/// </summary>
public sealed record RunRequest(string Task);

/// <summary>
/// Request model for the /chat endpoint
/// </summary>
public sealed record ChatRequest(string Message);
