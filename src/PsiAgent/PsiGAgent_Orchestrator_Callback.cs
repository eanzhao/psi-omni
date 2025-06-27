using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiAgent;

public partial class PsiGAgent
{
    private class OrchestrationDecisionPayload
    {
        [JsonPropertyName("decision")] public string Decision { get; set; }
        [JsonPropertyName("reasoning")] public string Reasoning { get; set; }
        [JsonPropertyName("new_tasks")] public List<NewSubTask> NewTasks { get; set; } = new();
        [JsonPropertyName("reply")] public string Reply { get; set; }
        [JsonPropertyName("cancel_subtask_id")] public string CancelSubTaskId { get; set; }
        [JsonPropertyName("followup_child_agent_id")] public string FollowUpChildAgentId { get; set; }
        [JsonPropertyName("followup_message")] public string FollowUpMessage { get; set; }
    }

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

    private async Task<(OrchestrationDecision, List<SubTask>, string, OrchestrationDecisionPayload)> DecideNextOrchestratorActionAsync()
    {
        var agentConfig = State.Configuration;
        var kernel = _kernelFactory.CreateKernel(agentConfig);
        var originalTask = State.Task;
        var chatHistory = State.Orchestrator.ConversationHistory;
        var currentSubTasks = State.Orchestrator.CurrentSubTasks;
        var completedCallbacks = State.Orchestrator.CompletedCallbacks;

        Logger.LogDebug("Deciding next orchestrator action using LLM");

        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var totalSubTasks = currentSubTasks.Count;
        var completedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Completed);
        var pendingSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Delegated);
        var notStartedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Pending);

        var pendingSubTaskDetails = currentSubTasks
            .Where(st => st.Status == SubTaskStatus.Delegated)
            .Select(st => $"- SubTaskId: {st.SubTaskId}, Task: {st.Task}, ChildAgentId: {st.ChildAgentId}, Status: {st.Status}, RequiredTools: [{string.Join(", ", st.RequiredTools)}], Priority: {st.Priority}, Dependencies: [{string.Join(", ", st.Dependencies)}], ReframedTask: {st.ReframedTask}")
            .ToList();
        var notStartedSubTaskDetails = currentSubTasks
            .Where(st => st.Status == SubTaskStatus.Pending)
            .Select(st => $"- SubTaskId: {st.SubTaskId}, Task: {st.Task}, ChildAgentId: {st.ChildAgentId}, Status: {st.Status}, RequiredTools: [{string.Join(", ", st.RequiredTools)}], Priority: {st.Priority}, Dependencies: [{string.Join(", ", st.Dependencies)}], ReframedTask: {st.ReframedTask}")
            .ToList();

        var jsonSchema = @"{
  ""decision"": ""COMPLETE_FINAL"" | ""COMPLETE_INTERMEDIATE"" | ""CREATE_ADDITIONAL_TASKS"" | ""WAIT_FOR_MORE_CALLBACKS"" | ""CANCEL_SUBTASK"" | ""FOLLOWUP_CHILD"",
  ""reasoning"": ""Your detailed analysis"",
  ""new_tasks"": [
        {""id"": ""temp-1"", ""task"": ""New subtask description 1"", ""dependencies"": [] },
        {""id"": ""temp-2"", ""task"": ""New subtask description 2"", ""dependencies"": [] },
        {""id"": ""temp-3"", ""task"": ""New subtask description 3"", ""dependencies"": [""temp-1"",""temp-2""] }
    ],
  ""reply"": ""Your next response to the user."",
  ""cancel_subtask_id"": ""(if decision is CANCEL_SUBTASK, the subtask id to cancel)"",
  ""followup_child_agent_id"": ""(if decision is FOLLOWUP_CHILD, the child agent id)"",
  ""followup_message"": ""(if decision is FOLLOWUP_CHILD, the message to send)""
}";

        var decisionPrompt = $@"
You are an expert orchestrator analyzing the progress of a complex, multi-round conversational task. Your goal is to decide the next best course of action.

Original Task: {originalTask}

Conversation History:
{string.Join("\n", chatHistory.Select(m => $"{m.Role}: {m.Content}"))}

Subtask Progress Summary:
- Completed: {completedSubTasks}/{totalSubTasks}
- In Progress (Delegated): {pendingSubTasks}
- Not Started: {notStartedSubTasks}

Pending Subtasks Details:
{string.Join("\n", pendingSubTaskDetails)}

Not Started Subtasks Details:
{string.Join("\n", notStartedSubTaskDetails)}

Based on the conversation and subtask progress, determine the next action.
- If all subtasks are complete and the original task is fulfilled, decide to 'COMPLETE_FINAL'.
- If all subtasks are complete but the conversation should continue, decide to 'COMPLETE_INTERMEDIATE'.
- If more subtasks are needed, decide to 'CREATE_ADDITIONAL_TASKS'.
- If you need to wait for pending subtasks, decide to 'WAIT_FOR_MORE_CALLBACKS'.
- If a pending or not started subtask should be canceled, decide to 'CANCEL_SUBTASK' and specify the subtask id.
- If a follow-up message should be sent to a child agent, decide to 'FOLLOWUP_CHILD' and specify the child agent id and the follow-up message.

Respond in JSON format with your decision, reasoning, a list of any new tasks, the next reply to the user, and if relevant, the subtask id to cancel or the child agent id and follow-up message.
" + jsonSchema;

        var result = await chatService.GetChatMessageContentAsync(decisionPrompt);
        var responseJson = result.Content ?? string.Empty;

        // Clean the response to ensure it's valid JSON, handling markdown fences
        var jsonStartIndex = responseJson.IndexOf('{');
        var jsonEndIndex = responseJson.LastIndexOf('}');
        if (jsonStartIndex != -1 && jsonEndIndex != -1)
        {
            responseJson = responseJson.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
        }

        OrchestrationDecisionPayload analysis;
        try
        {
            analysis = JsonSerializer.Deserialize<OrchestrationDecisionPayload>(responseJson.Trim());
        }
        catch (JsonException ex)
        {
            Logger.LogError(ex, "Failed to deserialize orchestration decision from LLM. Response: {response}", responseJson);
            return (OrchestrationDecision.WaitForMoreCallbacks, new List<SubTask>(), "I encountered an issue processing the last step. Please try again.", null);
        }

        var decision = analysis?.Decision?.ToUpperInvariant();
        var reply = analysis?.Reply ?? "Processing...";
        var newSubTasks = new List<SubTask>();
        OrchestrationDecision finalDecision;

        switch (decision)
        {
            case "COMPLETE_FINAL":
                finalDecision = OrchestrationDecision.CompleteTask;
                break;
            case "COMPLETE_INTERMEDIATE":
                finalDecision = OrchestrationDecision.ContinueConversation;
                break;
            case "CREATE_ADDITIONAL_TASKS":
                finalDecision = OrchestrationDecision.CreateAdditionalTasks;
                if (analysis.NewTasks != null && analysis.NewTasks.Any())
                {
                    var idMapping = new Dictionary<string, string>();
                    var currentTaskIds = State.Orchestrator.CurrentSubTasks.Select(t => t.SubTaskId).ToHashSet();

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
                            Dependencies = taskInfo.Dependencies
                        };
                        idMapping[taskInfo.Id] = newSubTask.SubTaskId;
                        newSubTasks.Add(newSubTask);
                        Logger.LogInformation("Created new subtask: {Description}", taskInfo.Task);
                    }

                    // Remap dependencies for newly created tasks
                    foreach (var subTask in newSubTasks)
                    {
                        subTask.Dependencies = subTask.Dependencies
                            .Select(depId => idMapping.TryGetValue(depId, out var newId) ? newId : depId)
                            .ToList();
                    }

                    newSubTasks = newSubTasks.Where(t => !currentTaskIds.Contains(t.SubTaskId)).ToList();
                }
                break;
            case "WAIT_FOR_MORE_CALLBACKS":
            case "CANCEL_SUBTASK":
            case "FOLLOWUP_CHILD":
                finalDecision = OrchestrationDecision.WaitForMoreCallbacks;
                break;
            default:
                finalDecision = OrchestrationDecision.WaitForMoreCallbacks;
                break;
        }
        
        // Enforce critical rule: if tasks are pending, we must wait.
        // if (pendingSubTasks > 0 && finalDecision != OrchestrationDecision.WaitForMoreCallbacks)
        // {
        //     Logger.LogWarning("Overriding LLM decision to WAIT because there are {Count} pending subtasks.", pendingSubTasks);
        //     finalDecision = OrchestrationDecision.WaitForMoreCallbacks;
        // }

        return (finalDecision, newSubTasks, reply, analysis);
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