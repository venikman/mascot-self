using System.ComponentModel.DataAnnotations;

namespace AgentLmLocal.Models;

/// <summary>
/// Request model for the /run endpoint
/// </summary>
public sealed record RunRequest(
    [Required(ErrorMessage = "Task description is required")]
    [StringLength(5000, MinimumLength = 1, ErrorMessage = "Task must be between 1 and 5000 characters")]
    string Task
);

/// <summary>
/// Request model for the /chat endpoint
/// </summary>
public sealed record ChatRequest(
    [Required(ErrorMessage = "Message is required")]
    [StringLength(2000, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 2000 characters")]
    string Message
);
