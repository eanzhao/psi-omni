using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiAgent;

public partial class PsiGAgent
{
    private class NewSubTask
    {
        [JsonPropertyName("id")] public string Id { get; set; }

        [JsonPropertyName("task")] public string Task { get; set; }

        [JsonPropertyName("suggestedTools")] public List<string> SuggestedTools { get; set; } = new();

        [JsonPropertyName("dependencies")] public List<string> Dependencies { get; set; } = new();
    }

    private class OrchestrationAnalysis
    {
        [JsonPropertyName("reasoning")] public string Reasoning { get; set; }

        [JsonPropertyName("decision")] public string Decision { get; set; }

        [JsonPropertyName("new_tasks")] public List<NewSubTask> NewTasks { get; set; }
    }

    private void UpdateDependencyResults(List<SubTask> allSubTasks, SubTask subTask, string result)
    {
        Logger.LogDebug("Updating dependency results for completed subtask {SubTaskId}", subTask.SubTaskId);

        // Find all subtasks that depend on this completed subtask
        var dependentSubTasks = allSubTasks
            .Where(st => st.Dependencies.Contains(subTask.SubTaskId))
            .ToList();

        foreach (var dependentSubTask in dependentSubTasks)
        {
            dependentSubTask.Dependencies.Remove(subTask.SubTaskId);
            // Add the result to the dependent subtask's dependency results
            dependentSubTask.DependencyResults[subTask.SubTaskId] = result;

            Logger.LogDebug("Added dependency result from {CompletedId} to {DependentId}",
                subTask.SubTaskId, dependentSubTask.SubTaskId);
        }

        foreach (var st in allSubTasks)
        {
            // A subtask can start if it has no dependencies
            st.CanStart = !st.Dependencies.Any();
        }

        Logger.LogInformation("Updated dependency results for {DependentCount} subtasks", dependentSubTasks.Count);
    }

    private async Task<(OrchestrationDecision, List<SubTask>)> AnalyzeProgressAsync()
    {
        var agentConfig = State.Configuration;
        var kernel = _kernelFactory.CreateKernel(agentConfig);
        var originalTask = State.Task;
        var currentSubTasks = State.Orchestrator.CurrentSubTasks;
        var completedCallbacks = State.Orchestrator.CompletedCallbacks;

        Logger.LogDebug("Analyzing progress with LLM for orchestration decision");

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var currentTaskIds = currentSubTasks.Select(t => t.SubTaskId).ToHashSet();
            // Gather current progress information
            var totalSubTasks = currentSubTasks.Count;
            var completedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Completed);
            var failedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Failed);
            var pendingSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Delegated);
            var notStartedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Pending);

            var completedResults = completedCallbacks
                .Where(cb => cb.IsSuccess)
                .Select(cb =>
                    $"<completed_subtask><id>{cb.CallId}</id><task>{cb.Task}</task><result>{cb.ResultMessage}</result></completed_subtask>")
                .ToList();

            var failedResults = completedCallbacks
                .Where(cb => !cb.IsSuccess)
                .Select(cb =>
                    $"<failed_subtask><id>{cb.CallId}</id><task>{cb.Task}</task><error>{cb.ResultMessage}</error></failed_subtask>")
                .ToList();

            var notStartedSubTaskDescriptions = currentSubTasks
                .Where(st => st.Status == SubTaskStatus.Pending)
                .Select(st =>
                    $"<not_started_subtask><id>{st.SubTaskId}</id><task>{st.Task}</task><dependencies>{string.Join(", ", st.Dependencies)}</dependencies></not_started_subtask>")
                .ToList();

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

            var analysisPrompt = $@"
You are an expert orchestrator analyzing the progress of a complex task. Your goal is to decide the next best course of action based on the status of subtasks.

Original Task: {originalTask}

**Progress Summary:**
- Total Subtasks: {totalSubTasks}
- Completed: {completedSubTasks}
- Failed: {failedSubTasks}
- In Progress (Delegated): {pendingSubTasks}
- Not Started: {notStartedSubTasks}

**Available tools (with descriptions):**
{toolListJson}

**Completed Subtask Results:**
{(completedResults.Any() ? string.Join("\n\n", completedResults) : "None")}

**Failed Subtask Results:**
{(failedResults.Any() ? string.Join("\n\n", failedResults) : "None")}

**Not Started Subtasks:**
{(notStartedSubTaskDescriptions.Any() ? string.Join("\n", notStartedSubTaskDescriptions) : "None")}

**Analysis and Decision:**

Based on the progress, analyze the situation and determine the next action.
- If results are sufficient, you can complete the task.
- If you need results from pending tasks, you must wait.
- If there are failures or gaps, you may need to create new tasks.
- Not-started tasks should be initiated if they are still relevant.

**Respond with a JSON object in the following format ONLY:**
{{
  ""reasoning"": ""<Your detailed analysis of the situation>"",
  ""decision"": ""<Choose one: COMPLETE, WAIT, CREATE_ADDITIONAL>"",
  ""new_tasks"": [
    {{
      ""id"": ""<new_task_id_1>"",
      ""task"": ""<Specific subtask description>"",
      ""suggestedTools"": [""<tool1>"", ""<tool2>""],
      ""dependencies"": [""<existing_or_new_task_id>""]
    }}
  ]
}}

**Decision Guide:**
- **COMPLETE**: The original task is fully addressed.
- **WAIT**: Crucial subtasks are still in progress.
- **CREATE_ADDITIONAL**: New tasks are needed. Provide their full details in `new_tasks`. This can include retrying failed tasks or starting tasks from the 'Not Started' list. The `dependencies` can refer to existing task IDs or other new task IDs from this list.
- If you decide `CREATE_ADDITIONAL`, there must be subtasks that have not started yet, otherwise you should suggest new tasks to be created.

**IMPORTANT RULES:**
- If there are pending subtasks (`In Progress > 0`), the decision MUST be `WAIT`.
- If there are no pending subtasks, but there are not-started tasks, the decision should likely be `CREATE_ADDITIONAL` to start them, unless analysis suggests they are no longer needed.
- If all tasks are completed, the decision should be `COMPLETE`.
- If you decide `CREATE_ADDITIONAL`, there must be outstanding subtasks that have not started or you have to create new tasks. If the task was given to you in the ""Not Started Subtasks"", don't include it in ""new_tasks"" array.
";

            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            var responseJson = result.Content ?? string.Empty;

            Logger.LogDebug("LLM analysis response: {Response}", responseJson);

            // Clean the response to ensure it's valid JSON, handling markdown fences
            var jsonStartIndex = responseJson.IndexOf('{');
            var jsonEndIndex = responseJson.LastIndexOf('}');
            if (jsonStartIndex != -1 && jsonEndIndex != -1)
            {
                responseJson = responseJson.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
            }

            OrchestrationAnalysis analysis;
            try
            {
                analysis = JsonSerializer.Deserialize<OrchestrationAnalysis>(responseJson.Trim());
            }
            catch (JsonException ex)
            {
                Logger.LogError(ex,
                    "Failed to deserialize orchestration analysis from LLM response. Response: {response}",
                    responseJson);
                // Fallback to a simple, safe logic
                if (pendingSubTasks > 0) return (OrchestrationDecision.WaitForMoreCallbacks, new List<SubTask>());
                if (notStartedSubTasks > 0) return (OrchestrationDecision.CreateAdditionalTasks, new List<SubTask>());
                return (totalSubTasks > 0 && completedSubTasks == totalSubTasks)
                    ? (OrchestrationDecision.CompleteTask, new List<SubTask>())
                    : (OrchestrationDecision.WaitForMoreCallbacks, new List<SubTask>());
            }

            var decision = analysis?.Decision?.ToUpperInvariant();
            Logger.LogInformation("LLM orchestration decision: {Decision}. Reasoning: {Reasoning}", decision,
                analysis?.Reasoning);
            var newSubTasks = new List<SubTask>();

            OrchestrationDecision finalDecision;
            switch (decision)
            {
                case "COMPLETE":
                    finalDecision = OrchestrationDecision.CompleteTask;
                    break;
                case "WAIT":
                    finalDecision = OrchestrationDecision.WaitForMoreCallbacks;
                    break;
                case "CREATE_ADDITIONAL":
                    finalDecision = OrchestrationDecision.CreateAdditionalTasks;
                    if (analysis.NewTasks != null && analysis.NewTasks.Any())
                    {
                        var idMapping = new Dictionary<string, string>();

                        Logger.LogInformation("LLM decided to create {Count} new subtasks.", analysis.NewTasks.Count);
                        foreach (var taskInfo in analysis.NewTasks)
                        {
                            var newSubTask = new SubTask
                            {
                                SubTaskId = currentTaskIds.Contains(taskInfo.Id)
                                    ? taskInfo.Id
                                    : Guid.NewGuid().ToString(),
                                Task = taskInfo.Task,
                                Status = SubTaskStatus.Pending,
                                RequiredTools = taskInfo.SuggestedTools,
                                Dependencies = taskInfo.Dependencies
                            };
                            idMapping[taskInfo.Id] = newSubTask.SubTaskId;
                            newSubTasks.Add(newSubTask);
                            Logger.LogInformation("Created new subtask: {Description}", taskInfo.Task);
                        }

                        // Remap dependencies for newly created tasks
                        foreach (var subTask in newSubTasks)
                        {
                            var newDeps = subTask.Dependencies
                                .Select(depId => idMapping.TryGetValue(depId, out var newId) ? newId : depId)
                                .ToList();
                            subTask.Dependencies = newDeps;
                        }
                    }
                    else
                    {
                        Logger.LogWarning("LLM decided CREATE_ADDITIONAL but provided no new tasks.");
                    }

                    break;
                default:
                    Logger.LogWarning("LLM returned an unknown or null decision: '{Decision}'. Defaulting to WAIT.",
                        decision);
                    finalDecision = OrchestrationDecision.WaitForMoreCallbacks;
                    break;
            }

            // Enforce critical rules, overriding the LLM if it makes a logical error.
            if (pendingSubTasks > 0 && finalDecision != OrchestrationDecision.WaitForMoreCallbacks)
            {
                Logger.LogWarning("Overriding LLM decision to WAIT because there are {Count} pending subtasks.",
                    pendingSubTasks);
                return (OrchestrationDecision.WaitForMoreCallbacks, new List<SubTask>());
            }

            if (pendingSubTasks == 0 && notStartedSubTasks == 0 && totalSubTasks > 0 &&
                completedSubTasks < totalSubTasks && finalDecision != OrchestrationDecision.CreateAdditionalTasks)
            {
                Logger.LogWarning(
                    "All tasks are finished but some failed, and the LLM did not suggest new tasks. Overriding to COMPLETE to avoid getting stuck.");
                return (OrchestrationDecision.CompleteTask, new List<SubTask>());
            }

            return (finalDecision, newSubTasks.Where(t => !currentTaskIds.Contains(t.SubTaskId)).ToList());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in LLM progress analysis");
            return (OrchestrationDecision.WaitForMoreCallbacks, new List<SubTask>()); // Safe default
        }
    }

    private async Task<string> AggregateResultsAsync()
    {
        var agentConfig = State.Configuration;
        var kernel = _kernelFactory.CreateKernel(agentConfig);
        var originalTask = State.Task;
        var completedCallbacks = State.Orchestrator.CompletedCallbacks;
        Logger.LogDebug("Aggregating results with LLM");

        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var successfulResults = completedCallbacks
                .Where(cb => cb.IsSuccess)
                .Select(cb => $"Subtask: {cb.Task}\nResult: {cb.ResultMessage}")
                .ToList();

            if (!successfulResults.Any())
            {
                Logger.LogWarning("No successful results to aggregate");
                return "No successful results to aggregate.";
            }

            var aggregationPrompt = $@"
You are aggregating the results of multiple subtasks that were part of a larger complex task.

Original Task: {originalTask}

Subtask Results:
{string.Join("\n\n---\n\n", successfulResults)}

Please create a coherent, comprehensive final response that:
1. Addresses the original task completely
2. Integrates insights from all subtask results
3. Provides a clear, actionable conclusion
4. Maintains consistency across all information
5. Synthesizes the information rather than just listing results

If there are any contradictions between subtask results, please resolve them logically and explain your reasoning.
If any subtask results seem incomplete or unclear, work with what is available and note any limitations.

Your response should be well-structured and directly answer the original task.";

            var result = await chatService.GetChatMessageContentAsync(aggregationPrompt);
            var aggregatedResult = result.Content ?? "Unable to aggregate results.";

            Logger.LogInformation("Successfully aggregated {ResultCount} subtask results", successfulResults.Count);
            return aggregatedResult;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in LLM result aggregation");

            // Fallback: simple concatenation
            var fallbackResult = CreateFallbackAggregation(originalTask, completedCallbacks);
            return fallbackResult;
        }
    }

    /// <summary>
    /// Create a simple fallback aggregation when LLM fails
    /// </summary>
    private string CreateFallbackAggregation(string originalTask, List<CompletedCallback> completedCallbacks)
    {
        var successfulResults = completedCallbacks
            .Where(cb => cb.IsSuccess)
            .ToList();

        if (!successfulResults.Any())
        {
            return "Task execution failed - no successful subtask results available.";
        }

        var fallback = $@"Task: {originalTask}

Results from {successfulResults.Count} completed subtasks:

{string.Join("\n\n", successfulResults.Select((cb, index) =>
    $"{index + 1}. {cb.Task}\n   Result: {cb.ResultMessage}"))}

Note: This is a basic aggregation due to processing limitations. Individual results are listed above.";

        Logger.LogWarning("Using fallback aggregation due to LLM error");
        return fallback;
    }
}