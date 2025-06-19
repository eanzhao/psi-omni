using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Models;

namespace PsiAgent;

public partial class PsiGAgent
{
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

    private async Task<OrchestrationDecision> AnalyzeProgressAsync()
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

            // Gather current progress information
            var totalSubTasks = currentSubTasks.Count;
            var completedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Completed);
            var failedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Failed);
            var pendingSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Delegated);
            var notStartedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Pending);

            var completedResults = completedCallbacks
                .Where(cb => cb.IsSuccess)
                .Select(cb => $"Task: {cb.Task}\nResult: {cb.ResultMessage}")
                .ToList();

            var failedResults = completedCallbacks
                .Where(cb => !cb.IsSuccess)
                .Select(cb => $"Failed Task: {cb.Task}\nError: {cb.ResultMessage}")
                .ToList();

            // Categorize failures by type
            var timeoutFailures = completedCallbacks
                .Where(cb =>
                    !cb.IsSuccess && (cb.ResultMessage.Contains("timeout") || cb.ResultMessage.Contains("timed out")))
                .Count();

            var otherFailures = failedSubTasks - timeoutFailures;

            var analysisPrompt = $@"
You are analyzing the progress of a complex task that has been broken down into subtasks and delegated to child agents.

Original Task: {originalTask}

Progress Status:
- Total subtasks: {totalSubTasks}
- Completed successfully: {completedSubTasks}
- Failed total: {failedSubTasks} (timeouts: {timeoutFailures}, other errors: {otherFailures})
- Still pending: {pendingSubTasks}
- Not started: {notStartedSubTasks}

Completed Results:
{string.Join("\n\n", completedResults)}

Failed Results:
{string.Join("\n\n", failedResults)}

Special Considerations:
- If there are timeout failures, they may be due to external API delays rather than task impossibility
- Timeout failures could potentially be retried with different approaches
- Consider if successful results are sufficient to answer the original task

Based on this progress, determine the next action:

1. COMPLETE - If you have enough successful results to satisfy the original task, even with some failures
2. WAIT - If you need to wait for more pending callbacks before deciding
3. CREATE_ADDITIONAL - If you need to create additional or retry subtasks to fill gaps
4. RETRY_TIMEOUTS - If timeout failures should be retried with simpler subtasks

Respond with exactly one word: COMPLETE, WAIT, CREATE_ADDITIONAL, or RETRY_TIMEOUTS

IMPORTANT: We cannot complete the task if there are still subtasks not started. We must not wait if there are no more pending subtasks.
";

            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            var decision = result.Content?.Trim().ToUpperInvariant();

            var orchestrationDecision = decision switch
            {
                "COMPLETE" => OrchestrationDecision.CompleteTask,
                "WAIT" => OrchestrationDecision.WaitForMoreCallbacks,
                "CREATE_ADDITIONAL" => OrchestrationDecision.CreateAdditionalTasks,
                "RETRY_TIMEOUTS" => OrchestrationDecision.RetryTimeouts,
                _ => OrchestrationDecision.WaitForMoreCallbacks // Default to waiting
            };

            Logger.LogInformation("LLM orchestration decision: {Decision}", orchestrationDecision);
            return orchestrationDecision;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in LLM progress analysis");
            return OrchestrationDecision.WaitForMoreCallbacks; // Safe default
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