using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using PsiOrleans.Common.Models;
using PsiOrleans.Plugins;
using PsiOrleans.Common.Interfaces;

namespace PsiOrleans.Orchestrator.Services;

/// <summary>
/// Service responsible for LLM-driven task decomposition.
/// Extracted from the real OrchestratorStateMachine.PlanDelegation method.
/// Framework-agnostic implementation for reusable orchestration logic.
/// </summary>
public interface ITaskDecomposer
{
    /// <summary>
    /// Break down a complex task into manageable subtasks using LLM.
    /// </summary>
    Task<List<SubTask>> DecomposeTaskAsync(string task, Kernel kernel);
}

/// <summary>
/// Implementation of LLM-based task decomposition.
/// Based on the working PlanDelegation logic from /src/Services/OrchestratorStateMachine.cs
/// </summary>
public class TaskDecomposer : ITaskDecomposer
{
    private readonly IKernelFactory _factory;
    private readonly ILogger<TaskDecomposer> _logger;

    public TaskDecomposer(ILogger<TaskDecomposer> logger, IKernelFactory factory)
    {
        _logger = logger;
        _factory = factory;
    }

    /// <summary>
    /// Use LLM to break down complex task into manageable subtasks.
    /// This is the foundation of orchestrator intelligence.
    /// </summary>
    public async Task<List<SubTask>> DecomposeTaskAsync(string task, Kernel kernel)
    {
        _logger.LogDebug("Planning task delegation using LLM");
        
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            // 获取所有可用工具名和描述
            var toolInfos = new List<string>();
            var registry = _factory.FunctionRegistry;
            if (registry != null)
            {
                foreach (var name in registry.GetAvailableFunctionNames())
                {
                    var func = registry.GetFunction(name);
                    var desc = func?.Description?.Replace("\"", "'") ?? "No description";
                    toolInfos.Add($"{{\"name\":\"{name}\",\"description\":\"{desc}\"}}");
                }
            }
            var toolListJson = "[" + string.Join(",", toolInfos) + "]";

            var delegationPrompt = $@"
You are an expert task orchestrator. Break down this complex task into 2-4 manageable subtasks that can be handled by specialized agents.

Original Task: {task}

Available tools (with descriptions): {toolListJson}

Requirements:
1. Each subtask should be specific and actionable
2. Identify dependencies between subtasks - which subtasks need results from other subtasks
3. Each subtask should be suitable for a specialized agent with focused tools
4. Provide a brief rationale for the breakdown
5. Use simple numeric IDs (1, 2, 3, etc.) for subtask identification

Example for ""What percentage of US GDP does New York represent?"":
- Subtask 1: Get US GDP data (no dependencies)
- Subtask 2: Get New York GDP data (no dependencies) 
- Subtask 3: Calculate percentage (depends on results from subtasks 1 and 2)

Respond in this exact JSON format:
{{
    ""subtasks"": [
        {{
            ""id"": ""1"",
            ""task"": ""Specific subtask description"",
            ""rationale"": ""Why this subtask is needed"",
            ""suggestedTools"": [""tool1"", ""tool2""],
            ""dependencies"": []
        }},
        {{
            ""id"": ""2"",
            ""task"": ""Another subtask description"",
            ""rationale"": ""Why this subtask is needed"",
            ""suggestedTools"": [""tool1""],
            ""dependencies"": []
        }},
        {{
            ""id"": ""3"",
            ""task"": ""Final calculation subtask"",
            ""rationale"": ""Combine results from previous subtasks"",
            ""suggestedTools"": [""Math.Divide""],
            ""dependencies"": [""1"", ""2""]
        }}
    ],
    ""overallStrategy"": ""Brief explanation of the delegation strategy and dependency flow""
}}
When choosing suggestedTools for each subtask, use both the name and the description to decide which are most relevant.";

            var result = await chatService.GetChatMessageContentAsync(delegationPrompt);
            var responseContent = result.Content ?? "";
            
            _logger.LogInformation("LLM delegation planning response: {Response}", responseContent);
            
            // Parse the LLM response to create SubTask objects with dependencies
            var subTasks = ParseDelegationResponse(responseContent);
            
            // Update CanStart status based on dependencies
            UpdateSubTaskStartability(subTasks);
            
            _logger.LogInformation("Planned {SubTaskCount} subtasks for delegation with dependencies", subTasks.Count);
            return subTasks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM task delegation planning");
            throw;
        }
    }

    /// <summary>
    /// Parse LLM response to extract subtasks with dependencies.
    /// Uses basic JSON parsing with fallback to text parsing.
    /// </summary>
    private List<SubTask> ParseDelegationResponse(string response)
    {
        try
        {
            // Try JSON parsing first
            var jsonDoc = JsonDocument.Parse(response);
            if (jsonDoc.RootElement.TryGetProperty("subtasks", out var subtasksElement))
            {
                var subTasks = new List<SubTask>();
                
                foreach (var subtaskElement in subtasksElement.EnumerateArray())
                {
                    var subTask = new SubTask
                    {
                        SubTaskId = subtaskElement.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                        Task = subtaskElement.GetProperty("task").GetString() ?? "",
                        SuggestedRole = AgentRole.Specialized,
                        Priority = 1,
                        Status = SubTaskStatus.Pending
                    };

                    // Parse suggested tools
                    if (subtaskElement.TryGetProperty("suggestedTools", out var toolsElement))
                    {
                        foreach (var tool in toolsElement.EnumerateArray())
                        {
                            var toolName = tool.GetString();
                            if (!string.IsNullOrEmpty(toolName))
                            {
                                subTask.RequiredTools.Add(toolName);
                            }
                        }
                    }

                    // Parse dependencies
                    if (subtaskElement.TryGetProperty("dependencies", out var depsElement))
                    {
                        foreach (var dep in depsElement.EnumerateArray())
                        {
                            var depId = dep.GetString();
                            if (!string.IsNullOrEmpty(depId))
                            {
                                subTask.Dependencies.Add(depId);
                            }
                        }
                    }

                    subTasks.Add(subTask);
                }

                return subTasks;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON response, falling back to basic parsing");
        }

        // Fallback: basic text parsing
        return CreateFallbackSubTasks(response);
    }

    /// <summary>
    /// Create basic subtasks when JSON parsing fails
    /// </summary>
    private List<SubTask> CreateFallbackSubTasks(string response)
    {
        // Simple fallback - create one subtask with the original response
        return new List<SubTask>
        {
            new SubTask
            {
                SubTaskId = "1",
                Task = "Analyze and process the original task",
                SuggestedRole = AgentRole.Specialized,
                RequiredTools = new List<string> { "Analysis", "Processing" },
                Priority = 1,
                Status = SubTaskStatus.Pending,
                Dependencies = new List<string>(),
                CanStart = true
            }
        };
    }

    /// <summary>
    /// Update CanStart status based on dependencies
    /// </summary>
    private void UpdateSubTaskStartability(List<SubTask> subTasks)
    {
        foreach (var subTask in subTasks)
        {
            // A subtask can start if it has no dependencies
            subTask.CanStart = !subTask.Dependencies.Any();
        }
    }
} 