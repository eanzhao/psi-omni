using Orleans;

namespace PsiGAgent.Common.Models;

/// <summary>
/// Represents the recommended approach for handling a task.
/// </summary>
public enum TaskApproach
{
    /// <summary>
    /// TaskApproach not known yet.
    /// </summary>
    Unknown,
    /// <summary>
    /// Task should be handled by direct execution (SPECIALIZED mode).
    /// </summary>
    DirectExecution,

    /// <summary>
    /// Task should be handled by orchestration (ORCHESTRATOR mode).
    /// </summary>
    Orchestration
}

/// <summary>
/// Represents the result of analyzing a task to determine the appropriate agent mode.
/// </summary>
[Serializable]
[GenerateSerializer]
public class TaskAnalysisResult
{
    /// <summary>
    /// Gets the recommended approach for handling this task.
    /// </summary>
    [Id(0)]
    public TaskApproach RecommendedApproach { get; init; }

    /// <summary>
    /// Gets whether this task can be broken down into smaller subtasks.
    /// True for ORCHESTRATOR mode, false for SPECIALIZED mode.
    /// </summary>
    [Id(1)]
    public bool CanBeDecomposed { get; init; }

    /// <summary>
    /// Gets any additional analysis notes or insights.
    /// </summary>
    [Id(2)]
    public string AnalysisNotes { get; init; } = string.Empty;

    /// <summary>
    /// Gets the recommended tools for handling this task.
    /// </summary>
    [Id(3)]
    public List<string> RecommendedTools { get; init; } = new();
}