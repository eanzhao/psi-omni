namespace PsiOrleans.Common.Models;

/// <summary>
/// Immutable value object representing agent performance metrics
/// </summary>
public class AgentMetrics : IEquatable<AgentMetrics>
{
    /// <summary>
    /// Total number of tasks attempted by the agent
    /// </summary>
    public int TotalTasks { get; private init; }

    /// <summary>
    /// Number of successfully completed tasks
    /// </summary>
    public int SuccessfulTasks { get; private init; }

    /// <summary>
    /// Number of failed tasks
    /// </summary>
    public int FailedTasks { get; private init; }

    /// <summary>
    /// Total execution time across all tasks
    /// </summary>
    public TimeSpan TotalExecutionTime { get; private init; }

    /// <summary>
    /// Success rate as a value between 0.0 and 1.0
    /// </summary>
    public double SuccessRate => TotalTasks > 0 ? (double)SuccessfulTasks / TotalTasks : 0.0;

    /// <summary>
    /// Average execution time per task
    /// </summary>
    public TimeSpan AverageExecutionTime => TotalTasks > 0 ? 
        TimeSpan.FromTicks(TotalExecutionTime.Ticks / TotalTasks) : 
        TimeSpan.Zero;

    /// <summary>
    /// Gets an empty metrics instance with all values set to zero.
    /// </summary>
    public static AgentMetrics Empty => new();

    /// <summary>
    /// Default constructor (all values zero)
    /// </summary>
    public AgentMetrics()
    {
        TotalTasks = 0;
        SuccessfulTasks = 0;
        FailedTasks = 0;
        TotalExecutionTime = TimeSpan.Zero;
    }

    /// <summary>
    /// Constructor with all values
    /// </summary>
    public AgentMetrics(int totalTasks, int successfulTasks, int failedTasks, TimeSpan totalExecutionTime)
    {
        // Validation
        if (totalTasks < 0)
            throw new ArgumentException("Total tasks cannot be negative", nameof(totalTasks));
        
        if (successfulTasks < 0)
            throw new ArgumentException("Successful tasks cannot be negative", nameof(successfulTasks));
        
        if (failedTasks < 0)
            throw new ArgumentException("Failed tasks cannot be negative", nameof(failedTasks));
        
        if (successfulTasks > totalTasks)
            throw new ArgumentException("Successful tasks cannot exceed total tasks", nameof(successfulTasks));
        
        if (failedTasks > totalTasks)
            throw new ArgumentException("Failed tasks cannot exceed total tasks", nameof(failedTasks));
        
        if (successfulTasks + failedTasks > totalTasks)
            throw new ArgumentException("Successful + failed tasks cannot exceed total tasks");

        TotalTasks = totalTasks;
        SuccessfulTasks = successfulTasks;
        FailedTasks = failedTasks;
        TotalExecutionTime = totalExecutionTime;
    }

    /// <summary>
    /// Creates a new AgentMetrics instance with the specified values.
    /// </summary>
    /// <param name="totalTasks">Total number of tasks attempted.</param>
    /// <param name="successfulTasks">Number of successfully completed tasks.</param>
    /// <param name="failedTasks">Number of failed tasks.</param>
    /// <param name="totalExecutionTime">Total execution time across all tasks.</param>
    /// <returns>A new AgentMetrics instance.</returns>
    public static AgentMetrics Create(int totalTasks, int successfulTasks, int failedTasks, TimeSpan totalExecutionTime)
    {
        return new AgentMetrics(totalTasks, successfulTasks, failedTasks, totalExecutionTime);
    }

    /// <summary>
    /// Returns a new instance with incremented successful task count
    /// </summary>
    public AgentMetrics IncrementSuccessful()
    {
        return new AgentMetrics(
            TotalTasks + 1,
            SuccessfulTasks + 1,
            FailedTasks,
            TotalExecutionTime);
    }

    /// <summary>
    /// Returns a new instance with incremented failed task count
    /// </summary>
    public AgentMetrics IncrementFailed()
    {
        return new AgentMetrics(
            TotalTasks + 1,
            SuccessfulTasks,
            FailedTasks + 1,
            TotalExecutionTime);
    }

    /// <summary>
    /// Returns a new instance with additional execution time
    /// </summary>
    public AgentMetrics AddExecutionTime(TimeSpan additionalTime)
    {
        return new AgentMetrics(
            TotalTasks,
            SuccessfulTasks,
            FailedTasks,
            TotalExecutionTime + additionalTime);
    }

    /// <summary>
    /// Value equality implementation
    /// </summary>
    public bool Equals(AgentMetrics? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return TotalTasks == other.TotalTasks &&
               SuccessfulTasks == other.SuccessfulTasks &&
               FailedTasks == other.FailedTasks &&
               TotalExecutionTime == other.TotalExecutionTime;
    }

    /// <summary>
    /// Object equality implementation
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is AgentMetrics other && Equals(other);
    }

    /// <summary>
    /// Hash code implementation
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(TotalTasks, SuccessfulTasks, FailedTasks, TotalExecutionTime);
    }

    /// <summary>
    /// Equality operator
    /// </summary>
    public static bool operator ==(AgentMetrics? left, AgentMetrics? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Inequality operator
    /// </summary>
    public static bool operator !=(AgentMetrics? left, AgentMetrics? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// String representation
    /// </summary>
    public override string ToString()
    {
        return $"Tasks: {SuccessfulTasks}/{TotalTasks} (Success Rate: {SuccessRate:P1}), " +
               $"Failed: {FailedTasks}, Avg Time: {AverageExecutionTime:hh\\:mm\\:ss}";
    }
} 