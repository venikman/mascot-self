using System.Text.Json.Serialization;

namespace WorkflowCustomAgentExecutorsSample.Models;

public enum NodeType
{
    ToolCall,
    LlmInvocation,
    Conditional
}

public enum ExecutionStatus
{
    Success,
    Failure,
    Partial
}

public enum RecoveryAction
{
    Retry,
    Rollback,
    Skip,
    Escalate
}

public enum ComplexityLevel
{
    Low,
    Medium,
    High
}

public static class EnumExtensions
{
    public static string ToLowerString(this NodeType type) => type.ToString().ToLowerInvariant();
    public static string ToLowerString(this ExecutionStatus status) => status.ToString().ToLowerInvariant();
    public static string ToLowerString(this RecoveryAction action) => action.ToString().ToLowerInvariant();
    public static string ToLowerString(this ComplexityLevel level) => level.ToString().ToLowerInvariant();

    public static ExecutionStatus ParseExecutionStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "success" => ExecutionStatus.Success,
            "failure" => ExecutionStatus.Failure,
            "partial" => ExecutionStatus.Partial,
            _ => ExecutionStatus.Failure
        };

    public static RecoveryAction ParseRecoveryAction(string action) =>
        action.ToLowerInvariant() switch
        {
            "retry" => RecoveryAction.Retry,
            "rollback" => RecoveryAction.Rollback,
            "skip" => RecoveryAction.Skip,
            "escalate" => RecoveryAction.Escalate,
            _ => RecoveryAction.Escalate
        };

    public static NodeType ParseNodeType(string type) =>
        type.ToLowerInvariant() switch
        {
            "tool_call" => NodeType.ToolCall,
            "llm_invocation" => NodeType.LlmInvocation,
            "conditional" => NodeType.Conditional,
            _ => NodeType.LlmInvocation
        };
}

public sealed class WorkflowNode
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; } // "tool_call", "llm_invocation", "conditional"

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = [];

    [JsonPropertyName("input_schema")]
    public Dictionary<string, string> InputSchema { get; set; } = [];

    [JsonPropertyName("output_schema")]
    public Dictionary<string, string> OutputSchema { get; set; } = [];
}

public sealed class WorkflowPlan
{
    [JsonPropertyName("task")]
    public required string Task { get; set; }

    [JsonPropertyName("nodes")]
    public List<WorkflowNode> Nodes { get; set; } = [];

    [JsonPropertyName("execution_order")]
    public List<string> ExecutionOrder { get; set; } = [];

    [JsonPropertyName("estimated_complexity")]
    public string EstimatedComplexity { get; set; } = "medium";
}

public sealed class ExecutionResult
{
    [JsonPropertyName("node_id")]
    public required string NodeId { get; set; }

    [JsonPropertyName("status")]
    public required string Status { get; set; } // "success", "failure", "partial"

    [JsonPropertyName("output")]
    public string Output { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("execution_time_ms")]
    public long ExecutionTimeMs { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public sealed class VerificationResult
{
    [JsonPropertyName("node_id")]
    public required string NodeId { get; set; }

    [JsonPropertyName("passed")]
    public required bool Passed { get; set; }

    [JsonPropertyName("quality_score")]
    public int QualityScore { get; set; } // 1-10

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = string.Empty;

    [JsonPropertyName("suggestions")]
    public List<string> Suggestions { get; set; } = [];

    [JsonPropertyName("requires_rollback")]
    public bool RequiresRollback { get; set; }
}

public sealed class RecoveryStrategy
{
    [JsonPropertyName("error_type")]
    public required string ErrorType { get; set; }

    [JsonPropertyName("root_cause")]
    public string RootCause { get; set; } = string.Empty;

    [JsonPropertyName("recovery_action")]
    public required string RecoveryAction { get; set; } // "retry", "rollback", "skip", "escalate"

    [JsonPropertyName("state_restoration_needed")]
    public bool StateRestorationNeeded { get; set; }

    [JsonPropertyName("alternative_path")]
    public List<string>? AlternativePath { get; set; }
}
