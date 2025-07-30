using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// Represents a workflow step that executes an MCP tool
/// </summary>
public class WorkflowStep
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("toolName")]
    [Required]
    public string ToolName { get; set; } = "";

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("dependsOn")]
    public List<string> DependsOn { get; set; } = new();

    [JsonPropertyName("continueOnError")]
    public bool ContinueOnError { get; set; } = false;

    [JsonPropertyName("timeout")]
    public TimeSpan? Timeout { get; set; }
}

/// <summary>
/// Defines a workflow containing multiple steps
/// </summary>
public class WorkflowDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("steps")]
    public List<WorkflowStep> Steps { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Represents the execution state of a workflow step
/// </summary>
public class WorkflowStepExecution
{
    [JsonPropertyName("stepId")]
    public string StepId { get; set; } = "";

    [JsonPropertyName("status")]
    public WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.Pending;

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
}

/// <summary>
/// Represents a workflow execution instance
/// </summary>
public class WorkflowExecution
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("workflowId")]
    public string WorkflowId { get; set; } = "";

    [JsonPropertyName("workflowName")]
    public string WorkflowName { get; set; } = "";

    [JsonPropertyName("status")]
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("stepExecutions")]
    public List<WorkflowStepExecution> StepExecutions { get; set; } = new();

    [JsonPropertyName("initiatedBy")]
    public string? InitiatedBy { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("results")]
    public Dictionary<string, object> Results { get; set; } = new();

    [JsonPropertyName("progress")]
    public WorkflowProgress Progress { get; set; } = new();

    [JsonPropertyName("duration")]
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
}

/// <summary>
/// Workflow progress tracking
/// </summary>
public class WorkflowProgress
{
    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; set; }

    [JsonPropertyName("completedSteps")]
    public int CompletedSteps { get; set; }

    [JsonPropertyName("failedSteps")]
    public int FailedSteps { get; set; }

    [JsonPropertyName("pendingSteps")]
    public int PendingSteps { get; set; }

    [JsonPropertyName("runningSteps")]
    public int RunningSteps { get; set; }

    [JsonPropertyName("percentComplete")]
    public double PercentComplete => TotalSteps > 0 ? (double)CompletedSteps / TotalSteps * 100 : 0;

    [JsonPropertyName("estimatedTimeRemaining")]
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// Batch processing configuration
/// </summary>
public class BatchProcessingConfig
{
    [JsonPropertyName("batchSize")]
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 10;

    [JsonPropertyName("maxParallelism")]
    [Range(1, 50)]
    public int MaxParallelism { get; set; } = 3;

    [JsonPropertyName("retryCount")]
    [Range(0, 10)]
    public int RetryCount { get; set; } = 2;

    [JsonPropertyName("retryDelay")]
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    [JsonPropertyName("continueOnError")]
    public bool ContinueOnError { get; set; } = true;
}

/// <summary>
/// Workflow execution status enumeration
/// </summary>
public enum WorkflowStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Workflow step execution status enumeration
/// </summary>
public enum WorkflowStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped,
    Cancelled
}

/// <summary>
/// Parameters for workflow definition tool
/// </summary>
public class WorkflowDefinitionParameters
{
    [JsonPropertyName("workflow")]
    [Required]
    public WorkflowDefinition Workflow { get; set; } = new();
}

/// <summary>
/// Parameters for workflow execution tool
/// </summary>
public class WorkflowExecutionParameters
{
    [JsonPropertyName("workflowId")]
    [Required]
    public string WorkflowId { get; set; } = "";

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("initiatedBy")]
    public string? InitiatedBy { get; set; }
}

/// <summary>
/// Parameters for workflow status tool
/// </summary>
public class WorkflowStatusParameters
{
    [JsonPropertyName("executionId")]
    [Required]
    public string ExecutionId { get; set; } = "";

    [JsonPropertyName("includeStepDetails")]
    public bool IncludeStepDetails { get; set; } = true;
}

/// <summary>
/// Parameters for batch processing tool
/// </summary>
public class BatchProcessingParameters
{
    [JsonPropertyName("workflowId")]
    [Required]
    public string WorkflowId { get; set; } = "";

    [JsonPropertyName("items")]
    [Required]
    public List<Dictionary<string, object>> Items { get; set; } = new();

    [JsonPropertyName("config")]
    public BatchProcessingConfig Config { get; set; } = new();

    [JsonPropertyName("initiatedBy")]
    public string? InitiatedBy { get; set; }
}

/// <summary>
/// Parameters for result aggregation tool
/// </summary>
public class ResultAggregationParameters
{
    [JsonPropertyName("executionIds")]
    [Required]
    public List<string> ExecutionIds { get; set; } = new();

    [JsonPropertyName("aggregationType")]
    public string AggregationType { get; set; } = "summary";

    [JsonPropertyName("includeDetails")]
    public bool IncludeDetails { get; set; } = false;
}

/// <summary>
/// Aggregated workflow results
/// </summary>
public class AggregatedResults
{
    [JsonPropertyName("totalExecutions")]
    public int TotalExecutions { get; set; }

    [JsonPropertyName("successfulExecutions")]
    public int SuccessfulExecutions { get; set; }

    [JsonPropertyName("failedExecutions")]
    public int FailedExecutions { get; set; }

    [JsonPropertyName("totalSteps")]
    public int TotalSteps { get; set; }

    [JsonPropertyName("successfulSteps")]
    public int SuccessfulSteps { get; set; }

    [JsonPropertyName("failedSteps")]
    public int FailedSteps { get; set; }

    [JsonPropertyName("averageDuration")]
    public TimeSpan? AverageDuration { get; set; }

    [JsonPropertyName("totalDuration")]
    public TimeSpan? TotalDuration { get; set; }

    [JsonPropertyName("successRate")]
    public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions * 100 : 0;

    [JsonPropertyName("results")]
    public List<WorkflowExecution> Results { get; set; } = new();

    [JsonPropertyName("summary")]
    public Dictionary<string, object> Summary { get; set; } = new();
}