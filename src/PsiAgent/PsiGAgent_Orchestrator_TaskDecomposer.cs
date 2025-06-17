using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Models;

namespace PsiAgent;

public partial class PsiGAgent
{
    private async Task<List<SubTask>> DecomposeTaskAsync()
    {
        var task = State.Task;
        var agentConfig = State.Configuration;
        var kernel = _kernelFactory.CreateKernel(agentConfig);

        Logger.LogDebug("Planning task delegation using LLM");

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            // 获取所有可用工具名和描述
            var toolInfos = new List<string>();
            var registry = _kernelFactory.FunctionRegistry;
            if (registry != null)
            {
                foreach (var name in registry.GetAllAvailableToolNames())
                {
                    var func = registry.GetToolByQualifiedName(name);
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

IMPORTANT: Make sure the json string has no comments.

When choosing suggestedTools for each subtask, use both the name and the description to decide which are most relevant.";

            var result = await chatService.GetChatMessageContentAsync(delegationPrompt);
            var responseContent = result.Content ?? "";

            Logger.LogInformation("LLM delegation planning response: {Response}", responseContent);

            // Parse the LLM response to create SubTask objects with dependencies
            var subTasks = ParseDelegationResponse(responseContent);

            // Update CanStart status based on dependencies
            UpdateSubTaskStartability(subTasks);

            Logger.LogInformation("Planned {SubTaskCount} subtasks for delegation with dependencies", subTasks.Count);
            return subTasks;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in LLM task delegation planning");
            throw;
        }
    }

    private List<SubTask> ParseDelegationResponse(string response)
    {
        try
        {
            // Try JSON parsing first
            var allIds = new Dictionary<string, string>();
            var jsonDoc = JsonDocument.Parse(response);
            if (jsonDoc.RootElement.TryGetProperty("subtasks", out var subtasksElement))
            {
                var subTasks = new List<SubTask>();

                foreach (var subtaskElement in subtasksElement.EnumerateArray())
                {
                    var subTask = new SubTask
                    {
                        SubTaskId = subtaskElement.GetProperty("id").GetString(),
                        Task = subtaskElement.GetProperty("task").GetString() ?? "",
                        SuggestedRole = AgentRole.Specialized,
                        Priority = 1,
                        Status = SubTaskStatus.Pending
                    };
                    allIds.TryAdd(subTask.SubTaskId, Guid.NewGuid().ToString());

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

                foreach (var subTask in subTasks)
                {
                    subTask.SubTaskId = allIds[subTask.SubTaskId];
                    var newDepIds = subTask.Dependencies.Select(x => allIds[x]).ToList();
                    subTask.Dependencies = newDepIds;
                }

                return subTasks;
            }
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Failed to parse JSON response, falling back to basic parsing");
        }

        // TODO: Handle failure
        return new List<SubTask>();
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