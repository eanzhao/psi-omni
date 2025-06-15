using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces;

/// <summary>
/// Defines the contract for analyzing tasks and breaking them down into manageable components.
/// Implemented by the Analysis package to provide task analysis capabilities.
/// </summary>
public interface ITaskAnalyzer
{
    /// <summary>
    /// Analyzes a task to determine its complexity, dependencies, and requirements.
    /// </summary>
    /// <param name="taskDescription">The description of the task to analyze.</param>
    /// <param name="context">The agent context providing analysis context.</param>
    /// <param name="configuration">The agent configuration needed for LLM-based analysis.</param>
    /// <returns>A detailed analysis result containing complexity, dependencies, recommendations, and LLM-recommended tools.</returns>
    Task<TaskAnalysisResult> AnalyzeTaskAsync(AgentState state);
} 