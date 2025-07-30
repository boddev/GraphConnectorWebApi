using System.Text.Json;
using ApiGraphActivator.McpTools;

namespace ApiGraphActivator.Services;

/// <summary>
/// Interface for workflow storage operations
/// </summary>
public interface IWorkflowStorageService
{
    // Workflow Definitions
    Task<string> SaveWorkflowDefinitionAsync(WorkflowDefinition workflow);
    Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string workflowId);
    Task<List<WorkflowDefinition>> GetWorkflowDefinitionsAsync();
    Task<bool> DeleteWorkflowDefinitionAsync(string workflowId);

    // Workflow Executions
    Task<string> SaveWorkflowExecutionAsync(WorkflowExecution execution);
    Task<WorkflowExecution?> GetWorkflowExecutionAsync(string executionId);
    Task<List<WorkflowExecution>> GetWorkflowExecutionsAsync(string? workflowId = null);
    Task<bool> UpdateWorkflowExecutionAsync(WorkflowExecution execution);

    // Progress and Status
    Task<List<WorkflowExecution>> GetRunningWorkflowsAsync();
    Task<List<WorkflowExecution>> GetWorkflowExecutionsByStatusAsync(WorkflowStatus status);
}

/// <summary>
/// In-memory implementation of workflow storage service
/// </summary>
public class InMemoryWorkflowStorageService : IWorkflowStorageService
{
    private readonly Dictionary<string, WorkflowDefinition> _workflows = new();
    private readonly Dictionary<string, WorkflowExecution> _executions = new();
    private readonly object _lock = new();

    public Task<string> SaveWorkflowDefinitionAsync(WorkflowDefinition workflow)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(workflow.Id))
            {
                workflow.Id = Guid.NewGuid().ToString();
            }
            workflow.CreatedAt = DateTime.UtcNow;
            _workflows[workflow.Id] = workflow;
            return Task.FromResult(workflow.Id);
        }
    }

    public Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string workflowId)
    {
        lock (_lock)
        {
            _workflows.TryGetValue(workflowId, out var workflow);
            return Task.FromResult(workflow);
        }
    }

    public Task<List<WorkflowDefinition>> GetWorkflowDefinitionsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_workflows.Values.ToList());
        }
    }

    public Task<bool> DeleteWorkflowDefinitionAsync(string workflowId)
    {
        lock (_lock)
        {
            return Task.FromResult(_workflows.Remove(workflowId));
        }
    }

    public Task<string> SaveWorkflowExecutionAsync(WorkflowExecution execution)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(execution.Id))
            {
                execution.Id = Guid.NewGuid().ToString();
            }
            _executions[execution.Id] = execution;
            return Task.FromResult(execution.Id);
        }
    }

    public Task<WorkflowExecution?> GetWorkflowExecutionAsync(string executionId)
    {
        lock (_lock)
        {
            _executions.TryGetValue(executionId, out var execution);
            return Task.FromResult(execution);
        }
    }

    public Task<List<WorkflowExecution>> GetWorkflowExecutionsAsync(string? workflowId = null)
    {
        lock (_lock)
        {
            var executions = _executions.Values.AsEnumerable();
            if (!string.IsNullOrEmpty(workflowId))
            {
                executions = executions.Where(e => e.WorkflowId == workflowId);
            }
            return Task.FromResult(executions.ToList());
        }
    }

    public Task<bool> UpdateWorkflowExecutionAsync(WorkflowExecution execution)
    {
        lock (_lock)
        {
            if (_executions.ContainsKey(execution.Id))
            {
                _executions[execution.Id] = execution;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<List<WorkflowExecution>> GetRunningWorkflowsAsync()
    {
        lock (_lock)
        {
            var running = _executions.Values
                .Where(e => e.Status == WorkflowStatus.Running)
                .ToList();
            return Task.FromResult(running);
        }
    }

    public Task<List<WorkflowExecution>> GetWorkflowExecutionsByStatusAsync(WorkflowStatus status)
    {
        lock (_lock)
        {
            var executions = _executions.Values
                .Where(e => e.Status == status)
                .ToList();
            return Task.FromResult(executions);
        }
    }
}

/// <summary>
/// File-based implementation of workflow storage service
/// </summary>
public class FileWorkflowStorageService : IWorkflowStorageService
{
    private readonly string _workflowsPath;
    private readonly string _executionsPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public FileWorkflowStorageService(string baseDirectory = "workflow-data")
    {
        _workflowsPath = Path.Combine(baseDirectory, "workflows");
        _executionsPath = Path.Combine(baseDirectory, "executions");
        
        Directory.CreateDirectory(_workflowsPath);
        Directory.CreateDirectory(_executionsPath);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task<string> SaveWorkflowDefinitionAsync(WorkflowDefinition workflow)
    {
        if (string.IsNullOrEmpty(workflow.Id))
        {
            workflow.Id = Guid.NewGuid().ToString();
        }
        workflow.CreatedAt = DateTime.UtcNow;

        var filePath = Path.Combine(_workflowsPath, $"{workflow.Id}.json");
        var json = JsonSerializer.Serialize(workflow, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        
        return workflow.Id;
    }

    public async Task<WorkflowDefinition?> GetWorkflowDefinitionAsync(string workflowId)
    {
        var filePath = Path.Combine(_workflowsPath, $"{workflowId}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<WorkflowDefinition>(json, _jsonOptions);
    }

    public async Task<List<WorkflowDefinition>> GetWorkflowDefinitionsAsync()
    {
        var workflows = new List<WorkflowDefinition>();
        var files = Directory.GetFiles(_workflowsPath, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var workflow = JsonSerializer.Deserialize<WorkflowDefinition>(json, _jsonOptions);
                if (workflow != null)
                {
                    workflows.Add(workflow);
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }

        return workflows;
    }

    public Task<bool> DeleteWorkflowDefinitionAsync(string workflowId)
    {
        var filePath = Path.Combine(_workflowsPath, $"{workflowId}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async Task<string> SaveWorkflowExecutionAsync(WorkflowExecution execution)
    {
        if (string.IsNullOrEmpty(execution.Id))
        {
            execution.Id = Guid.NewGuid().ToString();
        }

        var filePath = Path.Combine(_executionsPath, $"{execution.Id}.json");
        var json = JsonSerializer.Serialize(execution, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        
        return execution.Id;
    }

    public async Task<WorkflowExecution?> GetWorkflowExecutionAsync(string executionId)
    {
        var filePath = Path.Combine(_executionsPath, $"{executionId}.json");
        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<WorkflowExecution>(json, _jsonOptions);
    }

    public async Task<List<WorkflowExecution>> GetWorkflowExecutionsAsync(string? workflowId = null)
    {
        var executions = new List<WorkflowExecution>();
        var files = Directory.GetFiles(_executionsPath, "*.json");

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var execution = JsonSerializer.Deserialize<WorkflowExecution>(json, _jsonOptions);
                if (execution != null && (string.IsNullOrEmpty(workflowId) || execution.WorkflowId == workflowId))
                {
                    executions.Add(execution);
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }

        return executions;
    }

    public async Task<bool> UpdateWorkflowExecutionAsync(WorkflowExecution execution)
    {
        var filePath = Path.Combine(_executionsPath, $"{execution.Id}.json");
        if (File.Exists(filePath))
        {
            var json = JsonSerializer.Serialize(execution, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        return false;
    }

    public async Task<List<WorkflowExecution>> GetRunningWorkflowsAsync()
    {
        var allExecutions = await GetWorkflowExecutionsAsync();
        return allExecutions.Where(e => e.Status == WorkflowStatus.Running).ToList();
    }

    public async Task<List<WorkflowExecution>> GetWorkflowExecutionsByStatusAsync(WorkflowStatus status)
    {
        var allExecutions = await GetWorkflowExecutionsAsync();
        return allExecutions.Where(e => e.Status == status).ToList();
    }
}