namespace PsiOrleans.Common.Models;

/// <summary>
/// Represents the current status of an execution context.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Execution has been created but not yet started.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Execution has started and is currently running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Execution has been paused and can be resumed.
    /// </summary>
    Paused = 2,

    /// <summary>
    /// Execution has completed successfully.
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Execution has failed with an error.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Execution has been cancelled.
    /// </summary>
    Cancelled = 5
}

/// <summary>
/// Extension methods for ExecutionStatus.
/// </summary>
public static class ExecutionStatusExtensions
{
    /// <summary>
    /// Determines if the execution is in a finished state (completed, failed, or cancelled).
    /// </summary>
    /// <param name="status">The execution status to check.</param>
    /// <returns>True if the execution is finished, false otherwise.</returns>
    public static bool IsFinished(this ExecutionStatus status)
    {
        return status is ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled;
    }

    /// <summary>
    /// Determines if the execution is currently active (running or paused).
    /// </summary>
    /// <param name="status">The execution status to check.</param>
    /// <returns>True if the execution is active, false otherwise.</returns>
    public static bool IsActive(this ExecutionStatus status)
    {
        return status is ExecutionStatus.Running or ExecutionStatus.Paused;
    }

    /// <summary>
    /// Determines if the execution can be resumed (is paused).
    /// </summary>
    /// <param name="status">The execution status to check.</param>
    /// <returns>True if the execution can be resumed, false otherwise.</returns>
    public static bool CanBeResumed(this ExecutionStatus status)
    {
        return status == ExecutionStatus.Paused;
    }

    /// <summary>
    /// Determines if the execution completed successfully.
    /// </summary>
    /// <param name="status">The execution status to check.</param>
    /// <returns>True if the execution completed successfully, false otherwise.</returns>
    public static bool IsSuccessful(this ExecutionStatus status)
    {
        return status == ExecutionStatus.Completed;
    }
} 