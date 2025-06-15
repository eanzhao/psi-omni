namespace PsiOrleans.Common.Models;

/// <summary>
/// Represents the result of executing a specialized task.
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Gets whether the execution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the result data produced by the execution.
    /// </summary>
    public string Result { get; init; } = string.Empty;

    /// <summary>
    /// Gets the actual time taken to execute the task.
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets any error messages that occurred during execution.
    /// </summary>
    public IReadOnlyList<string> ErrorMessages { get; init; } = new List<string>();

    /// <summary>
    /// Gets any warnings generated during execution.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = new List<string>();

    /// <summary>
    /// Gets the specialization that was used for this execution.
    /// </summary>
    public string UsedSpecialization { get; init; } = string.Empty;

    /// <summary>
    /// Gets execution metrics and performance data.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metrics { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets any artifacts or files produced during execution.
    /// </summary>
    public IReadOnlyList<string> ProducedArtifacts { get; init; } = new List<string>();
} 