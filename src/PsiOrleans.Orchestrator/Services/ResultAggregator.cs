using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Orchestrator.Services;

/// <summary>
/// Service responsible for LLM-driven result aggregation.
/// Extracted from the real OrchestratorStateMachine.AggregateResultsWithLLM method.
/// Framework-agnostic implementation for intelligent result synthesis.
/// </summary>
public interface IResultAggregator
{
    /// <summary>
    /// Intelligently aggregate multiple subtask results into a coherent final result using LLM.
    /// </summary>
    Task<string> AggregateResultsAsync(
        string originalTask,
        List<CompletedCallback> completedCallbacks,
        Kernel kernel);
}

/// <summary>
/// Implementation of LLM-based result aggregation.
/// Based on the working AggregateResultsWithLLM logic from /src/Services/OrchestratorStateMachine.cs
/// </summary>
public class ResultAggregator : IResultAggregator
{
    private readonly ILogger<ResultAggregator> _logger;

    public ResultAggregator(ILogger<ResultAggregator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Use LLM to intelligently aggregate all child results into coherent final result.
    /// </summary>
    public async Task<string> AggregateResultsAsync(
        string originalTask,
        List<CompletedCallback> completedCallbacks,
        Kernel kernel)
    {
        _logger.LogDebug("Aggregating results with LLM");
        
        try
        {
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            
            var successfulResults = completedCallbacks
                .Where(cb => cb.IsSuccess)
                .Select(cb => $"Subtask: {cb.Task}\nResult: {cb.ResultMessage}")
                .ToList();
            
            if (!successfulResults.Any())
            {
                _logger.LogWarning("No successful results to aggregate");
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
            
            _logger.LogInformation("Successfully aggregated {ResultCount} subtask results", successfulResults.Count);
            return aggregatedResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LLM result aggregation");
            
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

        _logger.LogWarning("Using fallback aggregation due to LLM error");
        return fallback;
    }
} 