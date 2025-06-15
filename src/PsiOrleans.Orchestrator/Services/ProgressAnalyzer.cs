using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Orchestrator.Services;

/// <summary>
/// Service responsible for LLM-driven progress analysis and orchestration decision making.
/// Extracted from the real OrchestratorStateMachine.AnalyzeProgressWithLLM method.
/// Framework-agnostic implementation for reusable orchestration intelligence.
/// </summary>
public interface IProgressAnalyzer
{
    /// <summary>
    /// Analyze current orchestration progress and determine next action using LLM.
    /// </summary>
    Task<OrchestrationDecision> AnalyzeProgressAsync(
        string originalTask,
        List<SubTask> currentSubTasks,
        List<CompletedCallback> completedCallbacks,
        Kernel kernel);
}

/// <summary>
/// Implementation of LLM-based progress analysis.
/// Based on the working AnalyzeProgressWithLLM logic from /src/Services/OrchestratorStateMachine.cs
/// </summary>
public class ProgressAnalyzer : IProgressAnalyzer
{
    private readonly ILogger<ProgressAnalyzer> _logger;

    public ProgressAnalyzer(ILogger<ProgressAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Use LLM to analyze orchestration progress and determine next action.
    /// </summary>
    public async Task<OrchestrationDecision> AnalyzeProgressAsync(
        string originalTask,
        List<SubTask> currentSubTasks,
        List<CompletedCallback> completedCallbacks,
        Kernel kernel)
    {
        _logger.LogDebug("Analyzing progress with LLM for orchestration decision");
        
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            // Gather current progress information
            var totalSubTasks = currentSubTasks.Count;
            var completedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Completed);
            var failedSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Failed);
            var pendingSubTasks = currentSubTasks.Count(st => st.Status == SubTaskStatus.Delegated);
            
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
                .Where(cb => !cb.IsSuccess && (cb.ResultMessage.Contains("timeout") || cb.ResultMessage.Contains("timed out")))
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

Respond with exactly one word: COMPLETE, WAIT, CREATE_ADDITIONAL, or RETRY_TIMEOUTS";

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

            _logger.LogInformation("LLM orchestration decision: {Decision}", orchestrationDecision);
            return orchestrationDecision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM progress analysis");
            return OrchestrationDecision.WaitForMoreCallbacks; // Safe default
        }
    }
} 