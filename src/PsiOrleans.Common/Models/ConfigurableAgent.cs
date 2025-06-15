using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Immutable configurable agent that can orchestrate workflow steps.
/// Integrates AgentIdentity, AgentConfiguration, and AgentStep for comprehensive workflow management.
/// </summary>
[JsonConverter(typeof(ConfigurableAgentJsonConverter))]
public class ConfigurableAgent : IEquatable<ConfigurableAgent>
{
    /// <summary>
    /// Gets the identity of this agent.
    /// </summary>
    public AgentIdentity Identity { get; init; } = AgentIdentity.Empty;

    /// <summary>
    /// Gets the configuration for this agent.
    /// </summary>
    public AgentConfiguration Configuration { get; init; } = new AgentConfiguration();

    /// <summary>
    /// Gets the current execution context for this agent.
    /// </summary>
    public ExecutionContext Execution { get; init; } = ExecutionContext.Empty;

    /// <summary>
    /// Gets the workflow steps managed by this agent.
    /// </summary>
    public IReadOnlyList<AgentStep> WorkflowSteps { get; init; } = new List<AgentStep>();

    /// <summary>
    /// Gets the current active step being executed by this agent.
    /// </summary>
    public AgentStep? CurrentStep { get; init; }

    /// <summary>
    /// Gets the metrics for this agent.
    /// </summary>
    public AgentMetrics Metrics { get; init; } = AgentMetrics.Empty;

    /// <summary>
    /// Gets the agent's working memory for temporary data storage.
    /// </summary>
    public IReadOnlyDictionary<string, object> WorkingMemory { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the timestamp when this agent was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when this agent was last updated.
    /// </summary>
    public DateTime LastUpdated { get; init; }

    /// <summary>
    /// Gets whether this agent is currently executing a workflow step.
    /// </summary>
    public bool IsExecuting => CurrentStep?.IsRunning == true;

    /// <summary>
    /// Gets whether this agent has completed all workflow steps.
    /// </summary>
    public bool IsCompleted => WorkflowSteps.All(step => step.IsCompleted);

    /// <summary>
    /// Gets whether this agent has any failed workflow steps.
    /// </summary>
    public bool HasFailures => WorkflowSteps.Any(step => step.HasFailed);

    /// <summary>
    /// Gets the next available step that can be executed.
    /// </summary>
    public AgentStep? NextExecutableStep => WorkflowSteps
        .Where(step => step.CanExecute && !step.IsCompleted)
        .OrderByDescending(step => step.Priority)
        .ThenBy(step => step.CreatedAt)
        .FirstOrDefault();

    /// <summary>
    /// Gets the completion percentage of workflow steps.
    /// </summary>
    public double CompletionPercentage => WorkflowSteps.Count == 0 ? 0.0 : 
        (double)WorkflowSteps.Count(s => s.IsCompleted) / WorkflowSteps.Count;

    /// <summary>
    /// Private constructor for creating configurable agents.
    /// </summary>
    private ConfigurableAgent() { }

    /// <summary>
    /// Creates a new configurable agent with the specified parameters.
    /// </summary>
    /// <param name="identity">The agent identity.</param>
    /// <param name="configuration">The agent configuration.</param>
    /// <param name="execution">The execution context (optional, creates new if not provided).</param>
    /// <param name="workflowSteps">The workflow steps (optional).</param>
    /// <param name="currentStep">The current active step (optional).</param>
    /// <param name="metrics">The agent metrics (optional, creates empty if not provided).</param>
    /// <param name="workingMemory">The working memory (optional).</param>
    /// <param name="createdAt">The creation timestamp (optional, defaults to current time).</param>
    /// <param name="lastUpdated">The last update timestamp (optional, defaults to current time).</param>
    /// <returns>A new ConfigurableAgent instance.</returns>
    public static ConfigurableAgent Create(
        AgentIdentity identity,
        AgentConfiguration configuration,
        ExecutionContext? execution = null,
        IReadOnlyList<AgentStep>? workflowSteps = null,
        AgentStep? currentStep = null,
        AgentMetrics? metrics = null,
        IReadOnlyDictionary<string, object>? workingMemory = null,
        DateTime? createdAt = null,
        DateTime? lastUpdated = null)
    {
        var now = DateTime.UtcNow;
        var finalExecution = execution ?? ExecutionContext.Create(
            $"exec-{identity.Id}",
            identity.Id,
            $"Agent {identity.Name}",
            ExecutionStatus.Created,
            createdAt ?? now);

        return new ConfigurableAgent
        {
            Identity = identity,
            Configuration = configuration,
            Execution = finalExecution,
            WorkflowSteps = workflowSteps ?? new List<AgentStep>(),
            CurrentStep = currentStep,
            Metrics = metrics ?? AgentMetrics.Empty,
            WorkingMemory = workingMemory ?? new Dictionary<string, object>(),
            CreatedAt = createdAt ?? now,
            LastUpdated = lastUpdated ?? now
        };
    }

    /// <summary>
    /// Creates a new agent with updated identity.
    /// </summary>
    /// <param name="identity">The new agent identity.</param>
    /// <returns>A new ConfigurableAgent instance with updated identity.</returns>
    public ConfigurableAgent WithIdentity(AgentIdentity identity)
    {
        // Update execution context to match new agent identity
        var updatedExecution = Execution.AgentId != identity.Id
            ? ExecutionContext.Create(
                Execution.ExecutionId,
                identity.Id,
                Execution.TaskDescription,
                Execution.Status,
                Execution.StartedAt,
                Execution.CompletedAt,
                Execution.Metadata,
                Execution.ErrorMessage)
            : Execution;

        return Create(
            identity,
            Configuration,
            updatedExecution,
            WorkflowSteps,
            CurrentStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with updated configuration.
    /// </summary>
    /// <param name="configuration">The new agent configuration.</param>
    /// <returns>A new ConfigurableAgent instance with updated configuration.</returns>
    public ConfigurableAgent WithConfiguration(AgentConfiguration configuration)
    {
        return Create(
            Identity,
            configuration,
            Execution,
            WorkflowSteps,
            CurrentStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with updated execution context.
    /// </summary>
    /// <param name="execution">The new execution context.</param>
    /// <returns>A new ConfigurableAgent instance with updated execution context.</returns>
    public ConfigurableAgent WithExecution(ExecutionContext execution)
    {
        return Create(
            Identity,
            Configuration,
            execution,
            WorkflowSteps,
            CurrentStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with an added workflow step.
    /// </summary>
    /// <param name="step">The workflow step to add.</param>
    /// <returns>A new ConfigurableAgent instance with the added step.</returns>
    public ConfigurableAgent WithStep(AgentStep step)
    {
        var newSteps = new List<AgentStep>(WorkflowSteps) { step };
        return Create(
            Identity,
            Configuration,
            Execution,
            newSteps,
            CurrentStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with multiple added workflow steps.
    /// </summary>
    /// <param name="steps">The workflow steps to add.</param>
    /// <returns>A new ConfigurableAgent instance with the added steps.</returns>
    public ConfigurableAgent WithSteps(IEnumerable<AgentStep> steps)
    {
        var newSteps = new List<AgentStep>(WorkflowSteps);
        newSteps.AddRange(steps);
        return Create(
            Identity,
            Configuration,
            Execution,
            newSteps,
            CurrentStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with updated workflow steps.
    /// </summary>
    /// <param name="steps">The new workflow steps.</param>
    /// <returns>A new ConfigurableAgent instance with updated steps.</returns>
    public ConfigurableAgent WithWorkflowSteps(IReadOnlyList<AgentStep> steps)
    {
        return Create(
            Identity,
            Configuration,
            Execution,
            steps,
            CurrentStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with updated current step.
    /// </summary>
    /// <param name="currentStep">The new current step.</param>
    /// <returns>A new ConfigurableAgent instance with updated current step.</returns>
    public ConfigurableAgent WithCurrentStep(AgentStep? currentStep)
    {
        return Create(
            Identity,
            Configuration,
            Execution,
            WorkflowSteps,
            currentStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with updated metrics.
    /// </summary>
    /// <param name="metrics">The new agent metrics.</param>
    /// <returns>A new ConfigurableAgent instance with updated metrics.</returns>
    public ConfigurableAgent WithMetrics(AgentMetrics metrics)
    {
        return Create(
            Identity,
            Configuration,
            Execution,
            WorkflowSteps,
            CurrentStep,
            metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with updated working memory.
    /// </summary>
    /// <param name="workingMemory">The new working memory.</param>
    /// <returns>A new ConfigurableAgent instance with updated working memory.</returns>
    public ConfigurableAgent WithWorkingMemory(IReadOnlyDictionary<string, object> workingMemory)
    {
        return Create(
            Identity,
            Configuration,
            Execution,
            WorkflowSteps,
            CurrentStep,
            Metrics,
            workingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with an added working memory entry.
    /// </summary>
    /// <param name="key">The memory key.</param>
    /// <param name="value">The memory value.</param>
    /// <returns>A new ConfigurableAgent instance with the added memory entry.</returns>
    public ConfigurableAgent WithMemory(string key, object value)
    {
        var newMemory = new Dictionary<string, object>(WorkingMemory)
        {
            [key] = value
        };
        return WithWorkingMemory(newMemory);
    }

    /// <summary>
    /// Creates a new agent with a step marked as running.
    /// </summary>
    /// <param name="stepId">The ID of the step to mark as running.</param>
    /// <returns>A new ConfigurableAgent instance with the step marked as running.</returns>
    public ConfigurableAgent StartStep(string stepId)
    {
        var stepIndex = WorkflowSteps.ToList().FindIndex(s => s.StepId == stepId);
        if (stepIndex == -1)
        {
            return this; // Step not found, return unchanged
        }

        var step = WorkflowSteps[stepIndex];
        var runningStep = step.MarkRunning();
        var updatedSteps = WorkflowSteps.ToList();
        updatedSteps[stepIndex] = runningStep;

        return Create(
            Identity,
            Configuration,
            Execution,
            updatedSteps,
            runningStep,
            Metrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with a step marked as completed.
    /// </summary>
    /// <param name="stepId">The ID of the step to mark as completed.</param>
    /// <param name="outputs">Optional outputs from the step execution.</param>
    /// <returns>A new ConfigurableAgent instance with the step marked as completed.</returns>
    public ConfigurableAgent CompleteStep(string stepId, IReadOnlyDictionary<string, object>? outputs = null)
    {
        var stepIndex = WorkflowSteps.ToList().FindIndex(s => s.StepId == stepId);
        if (stepIndex == -1)
        {
            return this; // Step not found, return unchanged
        }

        var step = WorkflowSteps[stepIndex];
        var completedStep = step.MarkCompleted(outputs);
        var updatedSteps = WorkflowSteps.ToList();
        updatedSteps[stepIndex] = completedStep;

        // Update metrics
        var updatedMetrics = Metrics.IncrementSuccessful();

        // Clear current step if this was the current step
        var newCurrentStep = CurrentStep?.StepId == stepId ? null : CurrentStep;

        return Create(
            Identity,
            Configuration,
            Execution,
            updatedSteps,
            newCurrentStep,
            updatedMetrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent with a step marked as failed.
    /// </summary>
    /// <param name="stepId">The ID of the step to mark as failed.</param>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <returns>A new ConfigurableAgent instance with the step marked as failed.</returns>
    public ConfigurableAgent FailStep(string stepId, string errorMessage)
    {
        var stepIndex = WorkflowSteps.ToList().FindIndex(s => s.StepId == stepId);
        if (stepIndex == -1)
        {
            return this; // Step not found, return unchanged
        }

        var step = WorkflowSteps[stepIndex];
        var failedStep = step.MarkFailed(errorMessage);
        var updatedSteps = WorkflowSteps.ToList();
        updatedSteps[stepIndex] = failedStep;

        // Update metrics
        var updatedMetrics = Metrics.IncrementFailed();

        // Clear current step if this was the current step
        var newCurrentStep = CurrentStep?.StepId == stepId ? null : CurrentStep;

        return Create(
            Identity,
            Configuration,
            Execution,
            updatedSteps,
            newCurrentStep,
            updatedMetrics,
            WorkingMemory,
            CreatedAt,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Gets a workflow step by its ID.
    /// </summary>
    /// <param name="stepId">The step ID to find.</param>
    /// <returns>The step if found, otherwise null.</returns>
    public AgentStep? GetStep(string stepId)
    {
        return WorkflowSteps.FirstOrDefault(s => s.StepId == stepId);
    }

    /// <summary>
    /// Gets all workflow steps with the specified status.
    /// </summary>
    /// <param name="status">The execution status to filter by.</param>
    /// <returns>A list of steps with the specified status.</returns>
    public IList<AgentStep> GetStepsByStatus(ExecutionStatus status)
    {
        return WorkflowSteps.Where(s => s.Execution.Status == status).ToList();
    }

    /// <summary>
    /// Gets all pending (ready to execute) workflow steps.
    /// </summary>
    /// <returns>A list of pending steps.</returns>
    public IList<AgentStep> GetPendingSteps()
    {
        return WorkflowSteps.Where(s => s.CanExecute).ToList();
    }

    /// <summary>
    /// Gets an empty/invalid configurable agent.
    /// </summary>
    public static ConfigurableAgent Empty => new()
    {
        Identity = AgentIdentity.Empty,
        Configuration = new AgentConfiguration(),
        Execution = ExecutionContext.Empty,
        WorkflowSteps = new List<AgentStep>(),
        CurrentStep = null,
        Metrics = AgentMetrics.Empty,
        WorkingMemory = new Dictionary<string, object>(),
        CreatedAt = DateTime.MinValue,
        LastUpdated = DateTime.MinValue
    };

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(ConfigurableAgent? left, ConfigurableAgent? right) => 
        ReferenceEquals(left, right) || (left?.Equals(right) == true);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(ConfigurableAgent? left, ConfigurableAgent? right) => !(left == right);

    /// <summary>
    /// Checks equality with another ConfigurableAgent.
    /// </summary>
    /// <param name="other">The other ConfigurableAgent to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public bool Equals(ConfigurableAgent? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return Identity.Equals(other.Identity) &&
               Configuration.Equals(other.Configuration) &&
               Execution.Equals(other.Execution) &&
               WorkflowSteps.SequenceEqual(other.WorkflowSteps) &&
               Equals(CurrentStep, other.CurrentStep) &&
               Metrics.Equals(other.Metrics) &&
               DictionaryEquals(WorkingMemory, other.WorkingMemory) &&
               CreatedAt.Equals(other.CreatedAt) &&
               LastUpdated.Equals(other.LastUpdated);
    }

    /// <summary>
    /// Checks equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as ConfigurableAgent);
    }

    /// <summary>
    /// Gets the hash code for this ConfigurableAgent.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Identity);
        hash.Add(Configuration);
        hash.Add(Execution);
        hash.Add(CurrentStep);
        hash.Add(Metrics);
        hash.Add(CreatedAt);
        hash.Add(LastUpdated);
        
        // Add workflow steps to hash
        foreach (var step in WorkflowSteps.OrderBy(s => s.StepId))
        {
            hash.Add(step);
        }
        
        // Add working memory to hash
        foreach (var (key, value) in WorkingMemory.OrderBy(kv => kv.Key))
        {
            hash.Add(key, StringComparer.Ordinal);
            hash.Add(value);
        }
        
        return hash.ToHashCode();
    }

    /// <summary>
    /// Returns a string representation of this configurable agent.
    /// </summary>
    /// <returns>A formatted string containing the agent's key information.</returns>
    public override string ToString()
    {
        var stepInfo = WorkflowSteps.Count > 0 ? 
            $", Steps: {WorkflowSteps.Count} ({WorkflowSteps.Count(s => s.IsCompleted)} completed)" : "";
        var currentStepInfo = CurrentStep != null ? $", Current: {CurrentStep.StepId}" : "";
        return $"ConfigurableAgent(Id: {Identity.Id}, Name: {Identity.Name}, Role: {Identity.Role}{stepInfo}{currentStepInfo})";
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