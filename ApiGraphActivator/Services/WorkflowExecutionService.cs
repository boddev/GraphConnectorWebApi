using ApiGraphActivator.McpTools;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ApiGraphActivator.Services;

/// <summary>
/// Service for executing workflows
/// </summary>
public class WorkflowExecutionService
{
    private readonly IWorkflowStorageService _workflowStorage;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkflowExecutionService> _logger;
    private readonly Dictionary<string, Type> _mcpTools;

    public WorkflowExecutionService(
        IWorkflowStorageService workflowStorage,
        IServiceProvider serviceProvider,
        ILogger<WorkflowExecutionService> logger)
    {
        _workflowStorage = workflowStorage;
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Register available MCP tools
        _mcpTools = new Dictionary<string, Type>
        {
            { "company-search", typeof(CompanySearchTool) },
            { "form-filter", typeof(FormFilterTool) },
            { "content-search", typeof(ContentSearchTool) }
        };
    }

    /// <summary>
    /// Execute a workflow asynchronously
    /// </summary>
    public async Task<string> ExecuteWorkflowAsync(string workflowId, Dictionary<string, object> parameters, string? initiatedBy = null)
    {
        var workflow = await _workflowStorage.GetWorkflowDefinitionAsync(workflowId);
        if (workflow == null)
        {
            throw new ArgumentException($"Workflow with ID {workflowId} not found", nameof(workflowId));
        }

        var execution = new WorkflowExecution
        {
            WorkflowId = workflowId,
            WorkflowName = workflow.Name,
            Status = WorkflowStatus.Pending,
            InitiatedBy = initiatedBy,
            Parameters = parameters,
            Progress = new WorkflowProgress
            {
                TotalSteps = workflow.Steps.Count
            }
        };

        // Initialize step executions
        foreach (var step in workflow.Steps)
        {
            execution.StepExecutions.Add(new WorkflowStepExecution
            {
                StepId = step.Id,
                Status = WorkflowStepStatus.Pending
            });
        }

        var executionId = await _workflowStorage.SaveWorkflowExecutionAsync(execution);
        execution.Id = executionId;

        _logger.LogInformation("Starting workflow execution {ExecutionId} for workflow {WorkflowId}", executionId, workflowId);

        // Execute workflow in background
        _ = Task.Run(async () => await ExecuteWorkflowStepsAsync(execution, workflow));

        return executionId;
    }

    /// <summary>
    /// Execute workflow steps with proper dependency management
    /// </summary>
    private async Task ExecuteWorkflowStepsAsync(WorkflowExecution execution, WorkflowDefinition workflow)
    {
        try
        {
            execution.Status = WorkflowStatus.Running;
            execution.StartedAt = DateTime.UtcNow;
            await _workflowStorage.UpdateWorkflowExecutionAsync(execution);

            var executedSteps = new HashSet<string>();
            var stepResults = new Dictionary<string, object>();

            while (executedSteps.Count < workflow.Steps.Count)
            {
                var readySteps = workflow.Steps
                    .Where(step => !executedSteps.Contains(step.Id) && 
                                   step.DependsOn.All(dep => executedSteps.Contains(dep)))
                    .ToList();

                if (!readySteps.Any())
                {
                    // Check if there are any pending steps that haven't failed
                    var pendingSteps = workflow.Steps.Where(s => !executedSteps.Contains(s.Id)).ToList();
                    if (pendingSteps.Any())
                    {
                        _logger.LogError("Circular dependency or unresolvable dependencies detected in workflow {WorkflowId}", workflow.Id);
                        execution.Status = WorkflowStatus.Failed;
                        break;
                    }
                    break;
                }

                // Execute ready steps in parallel
                var stepTasks = readySteps.Select(step => ExecuteStepAsync(execution, step, stepResults));
                var stepResults_batch = await Task.WhenAll(stepTasks);

                foreach (var (step, result, success) in stepResults_batch)
                {
                    executedSteps.Add(step.Id);
                    if (result != null)
                    {
                        stepResults[step.Id] = result;
                        execution.Results[step.Id] = result;
                    }

                    if (!success && !step.ContinueOnError)
                    {
                        _logger.LogError("Step {StepId} failed and workflow is configured to stop on error", step.Id);
                        execution.Status = WorkflowStatus.Failed;
                        break;
                    }
                }

                if (execution.Status == WorkflowStatus.Failed)
                {
                    break;
                }

                // Update progress
                UpdateWorkflowProgress(execution);
                await _workflowStorage.UpdateWorkflowExecutionAsync(execution);
            }

            if (execution.Status != WorkflowStatus.Failed)
            {
                execution.Status = WorkflowStatus.Completed;
            }

            execution.CompletedAt = DateTime.UtcNow;
            UpdateWorkflowProgress(execution);
            await _workflowStorage.UpdateWorkflowExecutionAsync(execution);

            _logger.LogInformation("Workflow execution {ExecutionId} completed with status {Status}", 
                execution.Id, execution.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing workflow {WorkflowId}", workflow.Id);
            execution.Status = WorkflowStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;
            await _workflowStorage.UpdateWorkflowExecutionAsync(execution);
        }
    }

    /// <summary>
    /// Execute a single workflow step
    /// </summary>
    private async Task<(WorkflowStep step, object? result, bool success)> ExecuteStepAsync(
        WorkflowExecution execution, 
        WorkflowStep step, 
        Dictionary<string, object> stepResults)
    {
        var stepExecution = execution.StepExecutions.First(se => se.StepId == step.Id);
        
        try
        {
            _logger.LogInformation("Executing step {StepId} ({StepName}) in workflow {WorkflowId}", 
                step.Id, step.Name, execution.WorkflowId);

            stepExecution.Status = WorkflowStepStatus.Running;
            stepExecution.StartedAt = DateTime.UtcNow;

            // Resolve step parameters with previous step results and workflow parameters
            var resolvedParameters = ResolveStepParameters(step.Parameters, stepResults, execution.Parameters);

            // Execute the MCP tool
            var result = await ExecuteMcpToolAsync(step.ToolName, resolvedParameters);

            stepExecution.Status = WorkflowStepStatus.Completed;
            stepExecution.Result = result;
            stepExecution.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Step {StepId} completed successfully", step.Id);
            return (step, result, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Step {StepId} failed: {Error}", step.Id, ex.Message);
            
            stepExecution.Status = WorkflowStepStatus.Failed;
            stepExecution.Error = ex.Message;
            stepExecution.CompletedAt = DateTime.UtcNow;

            return (step, null, false);
        }
    }

    /// <summary>
    /// Resolve step parameters with variable substitution
    /// </summary>
    private Dictionary<string, object> ResolveStepParameters(
        Dictionary<string, object> stepParameters,
        Dictionary<string, object> stepResults,
        Dictionary<string, object> workflowParameters)
    {
        var resolved = new Dictionary<string, object>();

        foreach (var kvp in stepParameters)
        {
            var value = kvp.Value;
            
            // Handle string interpolation for parameter references
            if (value is string stringValue && stringValue.Contains("${"))
            {
                // Replace ${stepId.property} with actual values
                value = ResolveStringInterpolation(stringValue, stepResults, workflowParameters);
            }

            resolved[kvp.Key] = value;
        }

        return resolved;
    }

    /// <summary>
    /// Resolve string interpolation in parameters
    /// </summary>
    private string ResolveStringInterpolation(
        string input,
        Dictionary<string, object> stepResults,
        Dictionary<string, object> workflowParameters)
    {
        var result = input;

        // Simple pattern matching for ${variableName} or ${stepId.property}
        var pattern = @"\$\{([^}]+)\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(input, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var variablePath = match.Groups[1].Value;
            var value = ResolveVariablePath(variablePath, stepResults, workflowParameters);
            
            if (value != null)
            {
                result = result.Replace(match.Value, value.ToString());
            }
        }

        return result;
    }

    /// <summary>
    /// Resolve variable path like 'stepId.property' or 'workflow.parameter'
    /// </summary>
    private object? ResolveVariablePath(
        string path,
        Dictionary<string, object> stepResults,
        Dictionary<string, object> workflowParameters)
    {
        var parts = path.Split('.');
        
        if (parts.Length == 1)
        {
            // Simple workflow parameter
            workflowParameters.TryGetValue(parts[0], out var value);
            return value;
        }
        
        if (parts.Length >= 2)
        {
            var stepId = parts[0];
            var property = string.Join(".", parts.Skip(1));
            
            if (stepResults.TryGetValue(stepId, out var stepResult))
            {
                // Try to extract property from step result
                return ExtractPropertyFromObject(stepResult, property);
            }
        }

        return null;
    }

    /// <summary>
    /// Extract property from object using dot notation
    /// </summary>
    private object? ExtractPropertyFromObject(object obj, string propertyPath)
    {
        try
        {
            // Convert to JSON and back for property access
            var json = JsonSerializer.Serialize(obj);
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            
            var parts = propertyPath.Split('.');
            var current = jsonElement;
            
            foreach (var part in parts)
            {
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(part, out var property))
                {
                    current = property;
                }
                else
                {
                    return null;
                }
            }
            
            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString(),
                JsonValueKind.Number => current.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => current.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Execute an MCP tool with given parameters
    /// </summary>
    private async Task<object?> ExecuteMcpToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        if (!_mcpTools.TryGetValue(toolName, out var toolType))
        {
            throw new ArgumentException($"Unknown MCP tool: {toolName}");
        }

        var tool = _serviceProvider.GetService(toolType);
        if (tool == null)
        {
            throw new InvalidOperationException($"Could not resolve MCP tool: {toolName}");
        }

        // Convert parameters to appropriate type based on tool
        var parameterJson = JsonSerializer.Serialize(parameters);
        
        return toolType.Name switch
        {
            nameof(CompanySearchTool) => await ExecuteCompanySearchTool((CompanySearchTool)tool, parameterJson),
            nameof(FormFilterTool) => await ExecuteFormFilterTool((FormFilterTool)tool, parameterJson),
            nameof(ContentSearchTool) => await ExecuteContentSearchTool((ContentSearchTool)tool, parameterJson),
            _ => throw new ArgumentException($"Unsupported tool type: {toolType.Name}")
        };
    }

    private async Task<object> ExecuteCompanySearchTool(CompanySearchTool tool, string parametersJson)
    {
        var parameters = JsonSerializer.Deserialize<CompanySearchParameters>(parametersJson);
        return await tool.ExecuteAsync(parameters!);
    }

    private async Task<object> ExecuteFormFilterTool(FormFilterTool tool, string parametersJson)
    {
        var parameters = JsonSerializer.Deserialize<FormFilterParameters>(parametersJson);
        return await tool.ExecuteAsync(parameters!);
    }

    private async Task<object> ExecuteContentSearchTool(ContentSearchTool tool, string parametersJson)
    {
        var parameters = JsonSerializer.Deserialize<ContentSearchParameters>(parametersJson);
        return await tool.ExecuteAsync(parameters!);
    }

    /// <summary>
    /// Update workflow progress statistics
    /// </summary>
    private void UpdateWorkflowProgress(WorkflowExecution execution)
    {
        var progress = execution.Progress;
        progress.TotalSteps = execution.StepExecutions.Count;
        progress.CompletedSteps = execution.StepExecutions.Count(se => se.Status == WorkflowStepStatus.Completed);
        progress.FailedSteps = execution.StepExecutions.Count(se => se.Status == WorkflowStepStatus.Failed);
        progress.PendingSteps = execution.StepExecutions.Count(se => se.Status == WorkflowStepStatus.Pending);
        progress.RunningSteps = execution.StepExecutions.Count(se => se.Status == WorkflowStepStatus.Running);

        // Calculate estimated time remaining based on average step duration
        if (progress.CompletedSteps > 0 && execution.StartedAt.HasValue)
        {
            var elapsedTime = DateTime.UtcNow - execution.StartedAt.Value;
            var averageStepTime = elapsedTime.TotalSeconds / progress.CompletedSteps;
            var remainingSteps = progress.TotalSteps - progress.CompletedSteps;
            
            if (remainingSteps > 0)
            {
                progress.EstimatedTimeRemaining = TimeSpan.FromSeconds(averageStepTime * remainingSteps);
            }
        }
    }

    /// <summary>
    /// Cancel a running workflow execution
    /// </summary>
    public async Task<bool> CancelWorkflowAsync(string executionId)
    {
        var execution = await _workflowStorage.GetWorkflowExecutionAsync(executionId);
        if (execution == null || execution.Status != WorkflowStatus.Running)
        {
            return false;
        }

        execution.Status = WorkflowStatus.Cancelled;
        execution.CompletedAt = DateTime.UtcNow;

        // Cancel any pending or running steps
        foreach (var stepExecution in execution.StepExecutions)
        {
            if (stepExecution.Status == WorkflowStepStatus.Pending || stepExecution.Status == WorkflowStepStatus.Running)
            {
                stepExecution.Status = WorkflowStepStatus.Cancelled;
                stepExecution.CompletedAt = DateTime.UtcNow;
            }
        }

        await _workflowStorage.UpdateWorkflowExecutionAsync(execution);
        _logger.LogInformation("Workflow execution {ExecutionId} was cancelled", executionId);
        return true;
    }
}