using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Immutable workflow step that combines agent execution context with step-specific metadata.
/// Represents a single unit of work in an agent-based workflow system.
/// </summary>
[JsonConverter(typeof(AgentStepJsonConverter))]
public class AgentStep : IEquatable<AgentStep>
{
    /// <summary>
    /// Gets the unique identifier for this step.
    /// </summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the type of this workflow step.
    /// </summary>
    public StepType StepType { get; init; }

    /// <summary>
    /// Gets the identity of the agent responsible for executing this step.
    /// </summary>
    public AgentIdentity Agent { get; init; } = AgentIdentity.Empty;

    /// <summary>
    /// Gets the execution context for tracking step execution.
    /// </summary>
    public ExecutionContext Execution { get; init; } = ExecutionContext.Empty;

    /// <summary>
    /// Gets the instruction or description of what this step should accomplish.
    /// </summary>
    public string Instruction { get; init; } = string.Empty;

    /// <summary>
    /// Gets the input data for this step.
    /// </summary>
    public IReadOnlyDictionary<string, object> Inputs { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the output data produced by this step.
    /// </summary>
    public IReadOnlyDictionary<string, object> Outputs { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the list of step IDs that this step depends on.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = new List<string>();

    /// <summary>
    /// Gets the priority level of this step (higher values = higher priority).
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Gets the timestamp when this step was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets whether this step is valid and can be executed.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(StepId) && 
                          Agent.IsValid && 
                          !string.IsNullOrWhiteSpace(Instruction);

    /// <summary>
    /// Gets whether this step can be executed (has valid configuration and no blocking dependencies).
    /// </summary>
    public bool CanExecute => IsValid && Execution.Status == ExecutionStatus.Created;

    /// <summary>
    /// Gets whether this step is currently running.
    /// </summary>
    public bool IsRunning => Execution.IsRunning;

    /// <summary>
    /// Gets whether this step has completed execution.
    /// </summary>
    public bool IsCompleted => Execution.IsFinished;

    /// <summary>
    /// Gets whether this step completed successfully.
    /// </summary>
    public bool IsSuccessful => Execution.IsSuccessful;

    /// <summary>
    /// Gets whether this step has failed.
    /// </summary>
    public bool HasFailed => Execution.Status == ExecutionStatus.Failed;

    /// <summary>
    /// Private constructor for creating agent steps.
    /// </summary>
    private AgentStep() { }

    /// <summary>
    /// Creates a new agent step with the specified parameters.
    /// </summary>
    /// <param name="stepId">The unique step identifier.</param>
    /// <param name="stepType">The type of workflow step.</param>
    /// <param name="agent">The agent responsible for execution.</param>
    /// <param name="instruction">The step instruction or description.</param>
    /// <param name="inputs">Input data for the step (optional).</param>
    /// <param name="outputs">Output data from the step (optional).</param>
    /// <param name="dependencies">List of prerequisite step IDs (optional).</param>
    /// <param name="priority">Priority level (default: 0).</param>
    /// <param name="execution">Execution context (optional, creates new if not provided).</param>
    /// <param name="createdAt">Creation timestamp (optional, defaults to current time).</param>
    /// <returns>A new AgentStep instance.</returns>
    public static AgentStep Create(
        string stepId,
        StepType stepType,
        AgentIdentity agent,
        string instruction,
        IReadOnlyDictionary<string, object>? inputs = null,
        IReadOnlyDictionary<string, object>? outputs = null,
        IReadOnlyList<string>? dependencies = null,
        int priority = 0,
        ExecutionContext? execution = null,
        DateTime? createdAt = null)
    {
        var finalExecution = execution ?? ExecutionContext.Create(
            $"exec-{stepId}",
            agent.Id,
            instruction,
            ExecutionStatus.Created,
            createdAt ?? DateTime.UtcNow);

        return new AgentStep
        {
            StepId = stepId ?? string.Empty,
            StepType = stepType,
            Agent = agent,
            Execution = finalExecution,
            Instruction = instruction ?? string.Empty,
            Inputs = inputs ?? new Dictionary<string, object>(),
            Outputs = outputs ?? new Dictionary<string, object>(),
            Dependencies = dependencies ?? new List<string>(),
            Priority = priority,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new task step with the specified parameters.
    /// </summary>
    /// <param name="stepId">The unique step identifier.</param>
    /// <param name="agent">The agent responsible for execution.</param>
    /// <param name="instruction">The task instruction.</param>
    /// <param name="inputs">Input data for the task (optional).</param>
    /// <param name="priority">Priority level (default: 0).</param>
    /// <returns>A new AgentStep instance configured as a task.</returns>
    public static AgentStep CreateTask(
        string stepId,
        AgentIdentity agent,
        string instruction,
        IReadOnlyDictionary<string, object>? inputs = null,
        int priority = 0)
    {
        return Create(stepId, StepType.Task, agent, instruction, inputs, null, null, priority);
    }

    /// <summary>
    /// Creates a new decision step with the specified parameters.
    /// </summary>
    /// <param name="stepId">The unique step identifier.</param>
    /// <param name="agent">The agent responsible for execution.</param>
    /// <param name="instruction">The decision instruction.</param>
    /// <param name="inputs">Input data for the decision (optional).</param>
    /// <param name="priority">Priority level (default: 0).</param>
    /// <returns>A new AgentStep instance configured as a decision.</returns>
    public static AgentStep CreateDecision(
        string stepId,
        AgentIdentity agent,
        string instruction,
        IReadOnlyDictionary<string, object>? inputs = null,
        int priority = 0)
    {
        return Create(stepId, StepType.Decision, agent, instruction, inputs, null, null, priority);
    }

    /// <summary>
    /// Creates a new step with updated agent information.
    /// </summary>
    /// <param name="agent">The new agent identity.</param>
    /// <returns>A new AgentStep instance with updated agent.</returns>
    public AgentStep WithAgent(AgentIdentity agent)
    {
        var updatedExecution = Execution.AgentId != agent.Id 
            ? ExecutionContext.Create(
                Execution.ExecutionId,
                agent.Id,
                Execution.TaskDescription,
                Execution.Status,
                Execution.StartedAt,
                Execution.CompletedAt,
                Execution.Metadata,
                Execution.ErrorMessage)
            : Execution;

        return Create(
            StepId,
            StepType,
            agent,
            Instruction,
            Inputs,
            Outputs,
            Dependencies,
            Priority,
            updatedExecution,
            CreatedAt);
    }

    /// <summary>
    /// Creates a new step with updated execution context.
    /// </summary>
    /// <param name="execution">The new execution context.</param>
    /// <returns>A new AgentStep instance with updated execution context.</returns>
    public AgentStep WithExecution(ExecutionContext execution)
    {
        return Create(
            StepId,
            StepType,
            Agent,
            Instruction,
            Inputs,
            Outputs,
            Dependencies,
            Priority,
            execution,
            CreatedAt);
    }

    /// <summary>
    /// Creates a new step with an additional dependency.
    /// </summary>
    /// <param name="dependencyStepId">The ID of the step this step depends on.</param>
    /// <returns>A new AgentStep instance with the additional dependency.</returns>
    public AgentStep WithDependency(string dependencyStepId)
    {
        if (string.IsNullOrWhiteSpace(dependencyStepId) || Dependencies.Contains(dependencyStepId))
        {
            return this;
        }

        var newDependencies = new List<string>(Dependencies) { dependencyStepId };
        return Create(
            StepId,
            StepType,
            Agent,
            Instruction,
            Inputs,
            Outputs,
            newDependencies,
            Priority,
            Execution,
            CreatedAt);
    }

    /// <summary>
    /// Creates a new step with updated input data.
    /// </summary>
    /// <param name="inputs">The new input data.</param>
    /// <returns>A new AgentStep instance with updated inputs.</returns>
    public AgentStep WithInputs(IReadOnlyDictionary<string, object> inputs)
    {
        return Create(
            StepId,
            StepType,
            Agent,
            Instruction,
            inputs,
            Outputs,
            Dependencies,
            Priority,
            Execution,
            CreatedAt);
    }

    /// <summary>
    /// Creates a new step with an additional input entry.
    /// </summary>
    /// <param name="key">The input key.</param>
    /// <param name="value">The input value.</param>
    /// <returns>A new AgentStep instance with the additional input.</returns>
    public AgentStep WithInput(string key, object value)
    {
        var newInputs = new Dictionary<string, object>(Inputs)
        {
            [key] = value
        };
        return WithInputs(newInputs);
    }

    /// <summary>
    /// Creates a new step with updated output data.
    /// </summary>
    /// <param name="outputs">The new output data.</param>
    /// <returns>A new AgentStep instance with updated outputs.</returns>
    public AgentStep WithOutputs(IReadOnlyDictionary<string, object> outputs)
    {
        return Create(
            StepId,
            StepType,
            Agent,
            Instruction,
            Inputs,
            outputs,
            Dependencies,
            Priority,
            Execution,
            CreatedAt);
    }

    /// <summary>
    /// Creates a new step with an additional output entry.
    /// </summary>
    /// <param name="key">The output key.</param>
    /// <param name="value">The output value.</param>
    /// <returns>A new AgentStep instance with the additional output.</returns>
    public AgentStep WithOutput(string key, object value)
    {
        var newOutputs = new Dictionary<string, object>(Outputs)
        {
            [key] = value
        };
        return WithOutputs(newOutputs);
    }

    /// <summary>
    /// Creates a new step marked as ready for execution.
    /// </summary>
    /// <returns>A new AgentStep instance marked as ready.</returns>
    public AgentStep MarkReady()
    {
        var readyExecution = Execution.WithStatus(ExecutionStatus.Created);
        return WithExecution(readyExecution);
    }

    /// <summary>
    /// Creates a new step marked as running.
    /// </summary>
    /// <returns>A new AgentStep instance marked as running.</returns>
    public AgentStep MarkRunning()
    {
        var runningExecution = Execution.WithStatus(ExecutionStatus.Running);
        return WithExecution(runningExecution);
    }

    /// <summary>
    /// Creates a new step marked as completed with optional outputs.
    /// </summary>
    /// <param name="outputs">Output data from execution (optional).</param>
    /// <returns>A new AgentStep instance marked as completed.</returns>
    public AgentStep MarkCompleted(IReadOnlyDictionary<string, object>? outputs = null)
    {
        var completedExecution = Execution.WithCompleted();
        var step = WithExecution(completedExecution);
        return outputs != null ? step.WithOutputs(outputs) : step;
    }

    /// <summary>
    /// Creates a new step marked as failed with error information.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A new AgentStep instance marked as failed.</returns>
    public AgentStep MarkFailed(string errorMessage)
    {
        var failedExecution = Execution.WithError(errorMessage);
        return WithExecution(failedExecution);
    }

    /// <summary>
    /// Gets an empty/invalid agent step.
    /// </summary>
    public static AgentStep Empty => new()
    {
        StepId = string.Empty,
        StepType = StepType.Task,
        Agent = AgentIdentity.Empty,
        Execution = ExecutionContext.Empty,
        Instruction = string.Empty,
        Inputs = new Dictionary<string, object>(),
        Outputs = new Dictionary<string, object>(),
        Dependencies = new List<string>(),
        Priority = 0,
        CreatedAt = DateTime.MinValue
    };

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(AgentStep? left, AgentStep? right) => 
        ReferenceEquals(left, right) || (left?.Equals(right) == true);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(AgentStep? left, AgentStep? right) => !(left == right);

    /// <summary>
    /// Checks equality with another AgentStep.
    /// </summary>
    /// <param name="other">The other AgentStep to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public bool Equals(AgentStep? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return string.Equals(StepId, other.StepId, StringComparison.Ordinal) &&
               StepType == other.StepType &&
               Agent.Equals(other.Agent) &&
               Execution.Equals(other.Execution) &&
               string.Equals(Instruction, other.Instruction, StringComparison.Ordinal) &&
               DictionaryEquals(Inputs, other.Inputs) &&
               DictionaryEquals(Outputs, other.Outputs) &&
               Dependencies.SequenceEqual(other.Dependencies) &&
               Priority == other.Priority &&
               CreatedAt.Equals(other.CreatedAt);
    }

    /// <summary>
    /// Checks equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as AgentStep);
    }

    /// <summary>
    /// Gets the hash code for this AgentStep.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StepId, StringComparer.Ordinal);
        hash.Add(StepType);
        hash.Add(Agent);
        hash.Add(Execution);
        hash.Add(Instruction, StringComparer.Ordinal);
        hash.Add(Priority);
        hash.Add(CreatedAt);
        
        // Add inputs to hash
        foreach (var (key, value) in Inputs.OrderBy(kv => kv.Key))
        {
            hash.Add(key, StringComparer.Ordinal);
            hash.Add(value);
        }
        
        // Add outputs to hash
        foreach (var (key, value) in Outputs.OrderBy(kv => kv.Key))
        {
            hash.Add(key, StringComparer.Ordinal);
            hash.Add(value);
        }
        
        // Add dependencies to hash
        foreach (var dependency in Dependencies.OrderBy(d => d))
        {
            hash.Add(dependency, StringComparer.Ordinal);
        }
        
        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a string representation of this agent step.
    /// </summary>
    /// <returns>A formatted string containing the step's key information.</returns>
    public override string ToString()
    {
        var statusText = Execution.Status.ToString();
        var dependencyText = Dependencies.Any() ? $" (depends on: {string.Join(", ", Dependencies)})" : "";
        return $"AgentStep(Id: {StepId}, Type: {StepType}, Agent: {Agent.Name}, Status: {statusText}{dependencyText})";
    }

    /// <summary>
    /// Helper method to compare dictionaries.
    /// </summary>
    private static bool DictionaryEquals(IReadOnlyDictionary<string, object> left, IReadOnlyDictionary<string, object> right)
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