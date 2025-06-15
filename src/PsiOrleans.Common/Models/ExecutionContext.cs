using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Immutable execution context that captures task execution metadata and state.
/// Provides comprehensive tracking for agent task execution throughout the system.
/// </summary>
[JsonConverter(typeof(ExecutionContextJsonConverter))]
public class ExecutionContext : IEquatable<ExecutionContext>
{
    /// <summary>
    /// Gets the unique identifier for this execution.
    /// </summary>
    public string ExecutionId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the identifier of the agent performing this execution.
    /// </summary>
    public AgentId AgentId { get; init; }

    /// <summary>
    /// Gets the description of the task being executed.
    /// </summary>
    public string TaskDescription { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current status of the execution.
    /// </summary>
    public ExecutionStatus Status { get; init; }

    /// <summary>
    /// Gets the timestamp when execution started.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when execution completed (if finished).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Gets the duration of the execution (calculated if completed).
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>
    /// Gets the metadata associated with this execution.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets whether this execution context is valid.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ExecutionId) && 
                          AgentId.IsValid && 
                          !string.IsNullOrWhiteSpace(TaskDescription);

    /// <summary>
    /// Gets whether the execution is finished.
    /// </summary>
    public bool IsFinished => Status.IsFinished();

    /// <summary>
    /// Gets whether the execution is currently running.
    /// </summary>
    public bool IsRunning => Status == ExecutionStatus.Running;

    /// <summary>
    /// Gets whether the execution completed successfully.
    /// </summary>
    public bool IsSuccessful => Status.IsSuccessful();

    /// <summary>
    /// Private constructor for creating execution contexts.
    /// </summary>
    private ExecutionContext() { }

    /// <summary>
    /// Creates a new execution context with the specified parameters.
    /// </summary>
    /// <param name="executionId">The unique execution identifier.</param>
    /// <param name="agentId">The agent performing the execution.</param>
    /// <param name="taskDescription">Description of the task being executed.</param>
    /// <param name="status">The execution status.</param>
    /// <param name="startedAt">When the execution started.</param>
    /// <param name="completedAt">When the execution completed (optional).</param>
    /// <param name="metadata">Execution metadata (optional).</param>
    /// <param name="errorMessage">Error message if failed (optional).</param>
    /// <returns>A new ExecutionContext instance.</returns>
    public static ExecutionContext Create(
        string executionId,
        AgentId agentId,
        string taskDescription,
        ExecutionStatus status,
        DateTime startedAt,
        DateTime? completedAt = null,
        IReadOnlyDictionary<string, object>? metadata = null,
        string? errorMessage = null)
    {
        return new ExecutionContext
        {
            ExecutionId = executionId ?? string.Empty,
            AgentId = agentId,
            TaskDescription = taskDescription ?? string.Empty,
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Metadata = metadata ?? new Dictionary<string, object>(),
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a new execution context in the running state.
    /// </summary>
    /// <param name="agentId">The agent performing the execution.</param>
    /// <param name="taskDescription">Description of the task being executed.</param>
    /// <param name="metadata">Execution metadata (optional).</param>
    /// <returns>A new ExecutionContext instance with a generated execution ID and current timestamp.</returns>
    public static ExecutionContext CreateStarted(
        AgentId agentId,
        string taskDescription,
        IReadOnlyDictionary<string, object>? metadata = null)
    {
        var executionId = $"exec-{Guid.NewGuid():N}";
        return Create(executionId, agentId, taskDescription, ExecutionStatus.Running, DateTime.UtcNow, null, metadata);
    }

    /// <summary>
    /// Creates a new execution context with an updated status.
    /// </summary>
    /// <param name="status">The new execution status.</param>
    /// <param name="completedAt">Completion timestamp (optional, defaults to current time if status is finished).</param>
    /// <returns>A new ExecutionContext instance with the updated status.</returns>
    public ExecutionContext WithStatus(ExecutionStatus status, DateTime? completedAt = null)
    {
        var finalCompletedAt = status.IsFinished() ? (completedAt ?? DateTime.UtcNow) : (DateTime?)null;
        
        return Create(
            ExecutionId,
            AgentId,
            TaskDescription,
            status,
            StartedAt,
            finalCompletedAt,
            Metadata,
            ErrorMessage);
    }

    /// <summary>
    /// Creates a new execution context with completion status.
    /// </summary>
    /// <param name="completedAt">Completion timestamp (optional, defaults to current time).</param>
    /// <returns>A new ExecutionContext instance marked as completed.</returns>
    public ExecutionContext WithCompleted(DateTime? completedAt = null)
    {
        return WithStatus(ExecutionStatus.Completed, completedAt);
    }

    /// <summary>
    /// Creates a new execution context with failed status and error message.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="failedAt">Failure timestamp (optional, defaults to current time).</param>
    /// <returns>A new ExecutionContext instance marked as failed.</returns>
    public ExecutionContext WithError(string errorMessage, DateTime? failedAt = null)
    {
        return Create(
            ExecutionId,
            AgentId,
            TaskDescription,
            ExecutionStatus.Failed,
            StartedAt,
            failedAt ?? DateTime.UtcNow,
            Metadata,
            errorMessage);
    }

    /// <summary>
    /// Creates a new execution context with updated metadata.
    /// </summary>
    /// <param name="metadata">The new metadata dictionary.</param>
    /// <returns>A new ExecutionContext instance with updated metadata.</returns>
    public ExecutionContext WithMetadata(IReadOnlyDictionary<string, object> metadata)
    {
        return Create(
            ExecutionId,
            AgentId,
            TaskDescription,
            Status,
            StartedAt,
            CompletedAt,
            metadata,
            ErrorMessage);
    }

    /// <summary>
    /// Creates a new execution context with additional metadata entry.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new ExecutionContext instance with the additional metadata.</returns>
    public ExecutionContext WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata)
        {
            [key] = value
        };
        return WithMetadata(newMetadata);
    }

    /// <summary>
    /// Gets an empty/invalid execution context.
    /// </summary>
    public static ExecutionContext Empty => new()
    {
        ExecutionId = string.Empty,
        AgentId = AgentId.Empty,
        TaskDescription = string.Empty,
        Status = ExecutionStatus.Created,
        StartedAt = DateTime.MinValue,
        CompletedAt = null,
        Metadata = new Dictionary<string, object>(),
        ErrorMessage = null
    };

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(ExecutionContext? left, ExecutionContext? right) => 
        ReferenceEquals(left, right) || (left?.Equals(right) == true);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(ExecutionContext? left, ExecutionContext? right) => !(left == right);

    /// <summary>
    /// Checks equality with another ExecutionContext.
    /// </summary>
    /// <param name="other">The other ExecutionContext to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public bool Equals(ExecutionContext? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return string.Equals(ExecutionId, other.ExecutionId, StringComparison.Ordinal) &&
               AgentId.Equals(other.AgentId) &&
               string.Equals(TaskDescription, other.TaskDescription, StringComparison.Ordinal) &&
               Status == other.Status &&
               StartedAt.Equals(other.StartedAt) &&
               Nullable.Equals(CompletedAt, other.CompletedAt) &&
               MetadataEquals(Metadata, other.Metadata) &&
               string.Equals(ErrorMessage, other.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as ExecutionContext);
    }

    /// <summary>
    /// Gets the hash code for this ExecutionContext.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ExecutionId, StringComparer.Ordinal);
        hash.Add(AgentId);
        hash.Add(TaskDescription, StringComparer.Ordinal);
        hash.Add(Status);
        hash.Add(StartedAt);
        hash.Add(CompletedAt);
        hash.Add(ErrorMessage, StringComparer.Ordinal);
        
        // Add metadata to hash
        foreach (var (key, value) in Metadata.OrderBy(kv => kv.Key))
        {
            hash.Add(key, StringComparer.Ordinal);
            hash.Add(value);
        }
        
        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a string representation of this execution context.
    /// </summary>
    /// <returns>A formatted string containing the execution's key information.</returns>
    public override string ToString()
    {
        var durationText = Duration?.ToString(@"hh\:mm\:ss\.fff") ?? "N/A";
        return $"ExecutionContext(Id: {ExecutionId}, Agent: {AgentId}, Status: {Status}, Duration: {durationText})";
    }

    /// <summary>
    /// Helper method to compare metadata dictionaries.
    /// </summary>
    private static bool MetadataEquals(IReadOnlyDictionary<string, object> left, IReadOnlyDictionary<string, object> right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left.Count != right.Count) return false;
        
        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue) || !Equals(value, otherValue))
            {
                return false;
            }
        }
        
        return true;
    }
} 