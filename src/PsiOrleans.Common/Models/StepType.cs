namespace PsiOrleans.Common.Models;

/// <summary>
/// Represents the type of a workflow step.
/// </summary>
public enum StepType
{
    /// <summary>
    /// A basic task step that performs a single action.
    /// </summary>
    Task = 0,

    /// <summary>
    /// A decision step that branches workflow based on conditions.
    /// </summary>
    Decision = 1,

    /// <summary>
    /// A parallel step that can execute concurrently with other steps.
    /// </summary>
    Parallel = 2,

    /// <summary>
    /// A sequential step that must execute in order.
    /// </summary>
    Sequential = 3,

    /// <summary>
    /// A conditional step that executes only if conditions are met.
    /// </summary>
    Conditional = 4,

    /// <summary>
    /// An aggregation step that combines results from multiple steps.
    /// </summary>
    Aggregation = 5
}

/// <summary>
/// Extension methods for StepType.
/// </summary>
public static class StepTypeExtensions
{
    /// <summary>
    /// Determines if the step type can execute in parallel with others.
    /// </summary>
    /// <param name="stepType">The step type to check.</param>
    /// <returns>True if the step can execute in parallel, false otherwise.</returns>
    public static bool CanExecuteInParallel(this StepType stepType)
    {
        return stepType is StepType.Parallel or StepType.Task or StepType.Conditional;
    }

    /// <summary>
    /// Determines if the step type requires sequential execution.
    /// </summary>
    /// <param name="stepType">The step type to check.</param>
    /// <returns>True if the step requires sequential execution, false otherwise.</returns>
    public static bool RequiresSequentialExecution(this StepType stepType)
    {
        return stepType is StepType.Sequential or StepType.Decision;
    }

    /// <summary>
    /// Determines if the step type can have multiple outputs.
    /// </summary>
    /// <param name="stepType">The step type to check.</param>
    /// <returns>True if the step can have multiple outputs, false otherwise.</returns>
    public static bool CanHaveMultipleOutputs(this StepType stepType)
    {
        return stepType is StepType.Decision or StepType.Parallel or StepType.Aggregation;
    }

    /// <summary>
    /// Determines if the step type requires input validation.
    /// </summary>
    /// <param name="stepType">The step type to check.</param>
    /// <returns>True if the step requires input validation, false otherwise.</returns>
    public static bool RequiresInputValidation(this StepType stepType)
    {
        return stepType is StepType.Decision or StepType.Conditional or StepType.Aggregation;
    }

    /// <summary>
    /// Determines if the step type can be a workflow entry point.
    /// </summary>
    /// <param name="stepType">The step type to check.</param>
    /// <returns>True if the step can be a workflow entry point, false otherwise.</returns>
    public static bool CanBeEntryPoint(this StepType stepType)
    {
        return stepType is StepType.Task or StepType.Sequential or StepType.Parallel;
    }
} 