using ApiGraphActivator.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ApiGraphActivator.McpTools;

/// <summary>
/// MCP Tool for defining workflows
/// </summary>
public class WorkflowDefinitionTool : McpToolBase
{
    private readonly IWorkflowStorageService _workflowStorage;
    private readonly ILogger<WorkflowDefinitionTool> _logger;

    public WorkflowDefinitionTool(IWorkflowStorageService workflowStorage, ILogger<WorkflowDefinitionTool> logger)
    {
        _workflowStorage = workflowStorage;
        _logger = logger;
    }

    public override string Name => "workflow-definition";

    public override string Description => "Define and manage multi-step workflows that orchestrate MCP tools";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            workflow = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string", description = "Workflow name" },
                    description = new { type = "string", description = "Workflow description" },
                    steps = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                name = new { type = "string", description = "Step name" },
                                description = new { type = "string", description = "Step description" },
                                toolName = new { type = "string", description = "MCP tool to execute" },
                                parameters = new { type = "object", description = "Tool parameters" },
                                dependsOn = new
                                {
                                    type = "array",
                                    items = new { type = "string" },
                                    description = "Step IDs this step depends on"
                                },
                                continueOnError = new { type = "boolean", description = "Continue workflow if step fails" },
                                timeout = new { type = "string", description = "Step timeout (ISO 8601 duration)" }
                            },
                            required = new[] { "name", "toolName" }
                        }
                    },
                    tags = new
                    {
                        type = "array",
                        items = new { type = "string" },
                        description = "Workflow tags for categorization"
                    }
                },
                required = new[] { "name", "steps" }
            }
        },
        required = new[] { "workflow" }
    };

    public async Task<McpToolResponse<object>> ExecuteAsync(WorkflowDefinitionParameters parameters)
    {
        try
        {
            _logger.LogInformation("Creating workflow definition: {WorkflowName}", parameters.Workflow.Name);

            // Validate workflow definition
            var validationErrors = ValidateWorkflow(parameters.Workflow);
            if (validationErrors.Any())
            {
                return McpToolResponse<object>.Error($"Workflow validation failed: {string.Join(", ", validationErrors)}");
            }

            var workflowId = await _workflowStorage.SaveWorkflowDefinitionAsync(parameters.Workflow);
            
            var result = new
            {
                workflowId,
                name = parameters.Workflow.Name,
                steps = parameters.Workflow.Steps.Count,
                message = "Workflow definition created successfully"
            };

            _logger.LogInformation("Workflow definition created with ID: {WorkflowId}", workflowId);

            return McpToolResponse<object>.Success(result, new Dictionary<string, object>
            {
                { "workflowId", workflowId },
                { "createdAt", DateTime.UtcNow }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow definition");
            return McpToolResponse<object>.Error($"Failed to create workflow: {ex.Message}");
        }
    }

    private List<string> ValidateWorkflow(WorkflowDefinition workflow)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(workflow.Name))
        {
            errors.Add("Workflow name is required");
        }

        if (!workflow.Steps.Any())
        {
            errors.Add("Workflow must have at least one step");
        }

        // Check for duplicate step IDs
        var stepIds = workflow.Steps.Select(s => s.Id).ToList();
        var duplicateIds = stepIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var duplicateId in duplicateIds)
        {
            errors.Add($"Duplicate step ID: {duplicateId}");
        }

        // Validate step dependencies
        foreach (var step in workflow.Steps)
        {
            foreach (var dependency in step.DependsOn)
            {
                if (!stepIds.Contains(dependency))
                {
                    errors.Add($"Step '{step.Name}' depends on non-existent step ID: {dependency}");
                }
            }

            // Check for valid tool names
            var validTools = new[] { "company-search", "form-filter", "content-search" };
            if (!validTools.Contains(step.ToolName))
            {
                errors.Add($"Step '{step.Name}' uses invalid tool: {step.ToolName}");
            }
        }

        // Check for circular dependencies
        if (HasCircularDependencies(workflow.Steps))
        {
            errors.Add("Workflow contains circular dependencies");
        }

        return errors;
    }

    private bool HasCircularDependencies(List<WorkflowStep> steps)
    {
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var step in steps)
        {
            if (HasCircularDependencyRecursive(step.Id, steps, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasCircularDependencyRecursive(
        string stepId,
        List<WorkflowStep> steps,
        HashSet<string> visited,
        HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(stepId))
        {
            return true;
        }

        if (visited.Contains(stepId))
        {
            return false;
        }

        visited.Add(stepId);
        recursionStack.Add(stepId);

        var step = steps.FirstOrDefault(s => s.Id == stepId);
        if (step != null)
        {
            foreach (var dependency in step.DependsOn)
            {
                if (HasCircularDependencyRecursive(dependency, steps, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(stepId);
        return false;
    }
}

/// <summary>
/// MCP Tool for executing workflows
/// </summary>
public class WorkflowExecutionTool : McpToolBase
{
    private readonly WorkflowExecutionService _executionService;
    private readonly IWorkflowStorageService _workflowStorage;
    private readonly ILogger<WorkflowExecutionTool> _logger;

    public WorkflowExecutionTool(
        WorkflowExecutionService executionService,
        IWorkflowStorageService workflowStorage,
        ILogger<WorkflowExecutionTool> logger)
    {
        _executionService = executionService;
        _workflowStorage = workflowStorage;
        _logger = logger;
    }

    public override string Name => "workflow-execution";

    public override string Description => "Execute defined workflows with parameter substitution and progress tracking";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            workflowId = new { type = "string", description = "ID of the workflow to execute" },
            parameters = new
            {
                type = "object",
                description = "Parameters to pass to the workflow execution",
                additionalProperties = true
            },
            initiatedBy = new { type = "string", description = "User or system that initiated the workflow" }
        },
        required = new[] { "workflowId" }
    };

    public async Task<McpToolResponse<object>> ExecuteAsync(WorkflowExecutionParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting workflow execution for workflow: {WorkflowId}", parameters.WorkflowId);

            // Verify workflow exists
            var workflow = await _workflowStorage.GetWorkflowDefinitionAsync(parameters.WorkflowId);
            if (workflow == null)
            {
                return McpToolResponse<object>.Error($"Workflow with ID {parameters.WorkflowId} not found");
            }

            var executionId = await _executionService.ExecuteWorkflowAsync(
                parameters.WorkflowId,
                parameters.Parameters,
                parameters.InitiatedBy);

            var result = new
            {
                executionId,
                workflowId = parameters.WorkflowId,
                workflowName = workflow.Name,
                status = "started",
                message = "Workflow execution started successfully"
            };

            _logger.LogInformation("Workflow execution started with ID: {ExecutionId}", executionId);

            return McpToolResponse<object>.Success(result, new Dictionary<string, object>
            {
                { "executionId", executionId },
                { "startedAt", DateTime.UtcNow }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting workflow execution");
            return McpToolResponse<object>.Error($"Failed to start workflow execution: {ex.Message}");
        }
    }
}

/// <summary>
/// MCP Tool for monitoring workflow status
/// </summary>
public class WorkflowStatusTool : McpToolBase
{
    private readonly IWorkflowStorageService _workflowStorage;
    private readonly ILogger<WorkflowStatusTool> _logger;

    public WorkflowStatusTool(IWorkflowStorageService workflowStorage, ILogger<WorkflowStatusTool> logger)
    {
        _workflowStorage = workflowStorage;
        _logger = logger;
    }

    public override string Name => "workflow-status";

    public override string Description => "Monitor workflow execution progress and retrieve status information";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            executionId = new { type = "string", description = "Workflow execution ID to monitor" },
            includeStepDetails = new { type = "boolean", description = "Include detailed step execution information" }
        },
        required = new[] { "executionId" }
    };

    public async Task<McpToolResponse<object>> ExecuteAsync(WorkflowStatusParameters parameters)
    {
        try
        {
            _logger.LogInformation("Getting workflow status for execution: {ExecutionId}", parameters.ExecutionId);

            var execution = await _workflowStorage.GetWorkflowExecutionAsync(parameters.ExecutionId);
            if (execution == null)
            {
                return McpToolResponse<object>.Error($"Workflow execution with ID {parameters.ExecutionId} not found");
            }

            var result = new
            {
                executionId = execution.Id,
                workflowId = execution.WorkflowId,
                workflowName = execution.WorkflowName,
                status = execution.Status.ToString(),
                progress = execution.Progress,
                startedAt = execution.StartedAt,
                completedAt = execution.CompletedAt,
                duration = execution.Duration,
                initiatedBy = execution.InitiatedBy,
                stepDetails = parameters.IncludeStepDetails ? execution.StepExecutions : null
            };

            return McpToolResponse<object>.Success(result, new Dictionary<string, object>
            {
                { "retrievedAt", DateTime.UtcNow },
                { "isRunning", execution.Status == WorkflowStatus.Running }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow status");
            return McpToolResponse<object>.Error($"Failed to retrieve workflow status: {ex.Message}");
        }
    }
}

/// <summary>
/// MCP Tool for batch processing with workflows
/// </summary>
public class BatchProcessingTool : McpToolBase
{
    private readonly WorkflowExecutionService _executionService;
    private readonly IWorkflowStorageService _workflowStorage;
    private readonly ILogger<BatchProcessingTool> _logger;

    public BatchProcessingTool(
        WorkflowExecutionService executionService,
        IWorkflowStorageService workflowStorage,
        ILogger<BatchProcessingTool> logger)
    {
        _executionService = executionService;
        _workflowStorage = workflowStorage;
        _logger = logger;
    }

    public override string Name => "batch-processing";

    public override string Description => "Execute workflows in batch mode for multiple data items with parallel processing";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            workflowId = new { type = "string", description = "ID of the workflow to execute for each item" },
            items = new
            {
                type = "array",
                items = new { type = "object", additionalProperties = true },
                description = "Array of parameter objects, one for each workflow execution"
            },
            config = new
            {
                type = "object",
                properties = new
                {
                    batchSize = new { type = "integer", description = "Number of items to process in each batch" },
                    maxParallelism = new { type = "integer", description = "Maximum number of parallel executions" },
                    retryCount = new { type = "integer", description = "Number of retries for failed items" },
                    retryDelay = new { type = "string", description = "Delay between retries (ISO 8601 duration)" },
                    continueOnError = new { type = "boolean", description = "Continue processing if individual items fail" }
                }
            },
            initiatedBy = new { type = "string", description = "User or system that initiated the batch processing" }
        },
        required = new[] { "workflowId", "items" }
    };

    public async Task<McpToolResponse<object>> ExecuteAsync(BatchProcessingParameters parameters)
    {
        try
        {
            _logger.LogInformation("Starting batch processing for workflow: {WorkflowId} with {ItemCount} items", 
                parameters.WorkflowId, parameters.Items.Count);

            // Verify workflow exists
            var workflow = await _workflowStorage.GetWorkflowDefinitionAsync(parameters.WorkflowId);
            if (workflow == null)
            {
                return McpToolResponse<object>.Error($"Workflow with ID {parameters.WorkflowId} not found");
            }

            var batchId = Guid.NewGuid().ToString();
            var executionIds = new List<string>();

            // Process items in batches
            var batches = parameters.Items
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / parameters.Config.BatchSize)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            _logger.LogInformation("Processing {ItemCount} items in {BatchCount} batches of size {BatchSize}", 
                parameters.Items.Count, batches.Count, parameters.Config.BatchSize);

            var totalProcessed = 0;
            var totalSuccessful = 0;
            var totalFailed = 0;

            foreach (var batch in batches)
            {
                var batchTasks = batch.Take(parameters.Config.MaxParallelism).Select(async item =>
                {
                    try
                    {
                        var executionId = await _executionService.ExecuteWorkflowAsync(
                            parameters.WorkflowId,
                            item,
                            parameters.InitiatedBy);
                        
                        return new { ExecutionId = executionId, Success = true, Error = (string?)null };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start workflow execution for batch item");
                        return new { ExecutionId = (string?)null, Success = false, Error = ex.Message };
                    }
                });

                var batchResults = await Task.WhenAll(batchTasks);
                
                foreach (var batchResult in batchResults)
                {
                    totalProcessed++;
                    if (batchResult.Success && batchResult.ExecutionId != null)
                    {
                        executionIds.Add(batchResult.ExecutionId);
                        totalSuccessful++;
                    }
                    else
                    {
                        totalFailed++;
                        if (!parameters.Config.ContinueOnError)
                        {
                            _logger.LogError("Stopping batch processing due to error and continueOnError=false");
                            break;
                        }
                    }
                }

                if (totalFailed > 0 && !parameters.Config.ContinueOnError)
                {
                    break;
                }

                // Add delay between batches if configured
                if (parameters.Config.RetryDelay > TimeSpan.Zero)
                {
                    await Task.Delay(parameters.Config.RetryDelay);
                }
            }

            var result = new
            {
                batchId,
                workflowId = parameters.WorkflowId,
                workflowName = workflow.Name,
                totalItems = parameters.Items.Count,
                processedItems = totalProcessed,
                successfulExecutions = totalSuccessful,
                failedExecutions = totalFailed,
                executionIds,
                config = parameters.Config,
                startedAt = DateTime.UtcNow,
                message = "Batch processing initiated successfully"
            };

            _logger.LogInformation("Batch processing completed: {Successful}/{Total} executions started successfully", 
                totalSuccessful, totalProcessed);

            return McpToolResponse<object>.Success(result, new Dictionary<string, object>
            {
                { "batchId", batchId },
                { "totalExecutions", executionIds.Count }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch processing");
            return McpToolResponse<object>.Error($"Failed to process batch: {ex.Message}");
        }
    }
}

/// <summary>
/// MCP Tool for aggregating workflow results
/// </summary>
public class ResultAggregationTool : McpToolBase
{
    private readonly IWorkflowStorageService _workflowStorage;
    private readonly ILogger<ResultAggregationTool> _logger;

    public ResultAggregationTool(IWorkflowStorageService workflowStorage, ILogger<ResultAggregationTool> logger)
    {
        _workflowStorage = workflowStorage;
        _logger = logger;
    }

    public override string Name => "result-aggregation";

    public override string Description => "Aggregate and analyze results from multiple workflow executions";

    public override object InputSchema => new
    {
        type = "object",
        properties = new
        {
            executionIds = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Array of workflow execution IDs to aggregate"
            },
            aggregationType = new
            {
                type = "string",
                @enum = new[] { "summary", "detailed", "statistical" },
                description = "Type of aggregation to perform"
            },
            includeDetails = new { type = "boolean", description = "Include detailed execution information" }
        },
        required = new[] { "executionIds" }
    };

    public async Task<McpToolResponse<object>> ExecuteAsync(ResultAggregationParameters parameters)
    {
        try
        {
            _logger.LogInformation("Aggregating results for {ExecutionCount} workflow executions", 
                parameters.ExecutionIds.Count);

            var executions = new List<WorkflowExecution>();
            
            foreach (var executionId in parameters.ExecutionIds)
            {
                var execution = await _workflowStorage.GetWorkflowExecutionAsync(executionId);
                if (execution != null)
                {
                    executions.Add(execution);
                }
            }

            if (!executions.Any())
            {
                return McpToolResponse<object>.Error("No valid workflow executions found for the provided IDs");
            }

            var aggregatedResults = new AggregatedResults
            {
                TotalExecutions = executions.Count,
                SuccessfulExecutions = executions.Count(e => e.Status == WorkflowStatus.Completed),
                FailedExecutions = executions.Count(e => e.Status == WorkflowStatus.Failed),
                TotalSteps = executions.Sum(e => e.StepExecutions.Count),
                SuccessfulSteps = executions.Sum(e => e.StepExecutions.Count(se => se.Status == WorkflowStepStatus.Completed)),
                FailedSteps = executions.Sum(e => e.StepExecutions.Count(se => se.Status == WorkflowStepStatus.Failed)),
                Results = parameters.IncludeDetails ? executions : new List<WorkflowExecution>()
            };

            // Calculate timing statistics
            var completedExecutions = executions.Where(e => e.Duration.HasValue).ToList();
            if (completedExecutions.Any())
            {
                aggregatedResults.AverageDuration = TimeSpan.FromTicks((long)completedExecutions.Average(e => e.Duration!.Value.Ticks));
                aggregatedResults.TotalDuration = TimeSpan.FromTicks(completedExecutions.Sum(e => e.Duration!.Value.Ticks));
            }

            // Generate summary based on aggregation type
            aggregatedResults.Summary = parameters.AggregationType switch
            {
                "detailed" => GenerateDetailedSummary(executions),
                "statistical" => GenerateStatisticalSummary(executions),
                _ => GenerateBasicSummary(executions)
            };

            _logger.LogInformation("Result aggregation completed: {Successful}/{Total} executions were successful", 
                aggregatedResults.SuccessfulExecutions, aggregatedResults.TotalExecutions);

            return McpToolResponse<object>.Success(aggregatedResults, new Dictionary<string, object>
            {
                { "aggregatedAt", DateTime.UtcNow },
                { "aggregationType", parameters.AggregationType }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error aggregating workflow results");
            return McpToolResponse<object>.Error($"Failed to aggregate results: {ex.Message}");
        }
    }

    private Dictionary<string, object> GenerateBasicSummary(List<WorkflowExecution> executions)
    {
        return new Dictionary<string, object>
        {
            { "totalExecutions", executions.Count },
            { "successRate", executions.Count > 0 ? (double)executions.Count(e => e.Status == WorkflowStatus.Completed) / executions.Count * 100 : 0 },
            { "averageStepsPerWorkflow", executions.Count > 0 ? executions.Average(e => e.StepExecutions.Count) : 0 },
            { "mostCommonWorkflow", executions.GroupBy(e => e.WorkflowName).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? "N/A" }
        };
    }

    private Dictionary<string, object> GenerateDetailedSummary(List<WorkflowExecution> executions)
    {
        var summary = GenerateBasicSummary(executions);
        
        summary.Add("executionsByStatus", executions.GroupBy(e => e.Status).ToDictionary(g => g.Key.ToString(), g => g.Count()));
        summary.Add("executionsByWorkflow", executions.GroupBy(e => e.WorkflowName).ToDictionary(g => g.Key, g => g.Count()));
        summary.Add("stepSuccessRates", executions
            .SelectMany(e => e.StepExecutions)
            .GroupBy(se => se.StepId)
            .ToDictionary(g => g.Key, g => g.Count(se => se.Status == WorkflowStepStatus.Completed) / (double)g.Count() * 100));

        return summary;
    }

    private Dictionary<string, object> GenerateStatisticalSummary(List<WorkflowExecution> executions)
    {
        var summary = GenerateDetailedSummary(executions);
        
        var durations = executions.Where(e => e.Duration.HasValue).Select(e => e.Duration!.Value.TotalSeconds).ToList();
        if (durations.Any())
        {
            summary.Add("durationStatistics", new
            {
                min = durations.Min(),
                max = durations.Max(),
                average = durations.Average(),
                median = durations.OrderBy(d => d).ElementAt(durations.Count / 2),
                standardDeviation = Math.Sqrt(durations.Select(d => Math.Pow(d - durations.Average(), 2)).Average())
            });
        }

        return summary;
    }
}