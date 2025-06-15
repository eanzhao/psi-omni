using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces;

/// <summary>
/// Defines the contract for executing domain-specific specialized tasks.
/// Implemented by the Specialized package to provide specialized execution capabilities.
/// </summary>
public interface ISpecializedExecutor
{
    /// <summary>
    /// Gets the specialization type that this executor handles.
    /// </summary>
    string Specialization { get; }

    /// <summary>
    /// Executes a specialized task using domain-specific knowledge and capabilities.
    /// </summary>
    /// <param name="taskDescription">The description of the task to execute.</param>
    /// <param name="context">The agent context providing execution context.</param>
    /// <returns>The result of the specialized execution including success status and output.</returns>
    Task<ExecutionResult> ExecuteTaskAsync(string taskDescription, IAgentContext context);

    /// <summary>
    /// Determines whether this executor can handle the specified task.
    /// </summary>
    /// <param name="taskDescription">The description of the task to evaluate.</param>
    /// <param name="context">The agent context providing evaluation context.</param>
    /// <returns>True if this executor can handle the task, false otherwise.</returns>
    bool CanHandleTask(string taskDescription, IAgentContext context);
} 