using Microsoft.Extensions.Logging;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Orchestrator.Services;

/// <summary>
/// Service responsible for managing subtask dependencies.
/// Extracted from the real OrchestratorStateMachine dependency management methods.
/// Framework-agnostic implementation for dependency tracking and resolution.
/// </summary>
public interface IDependencyManager
{
    /// <summary>
    /// Update dependency results when a subtask completes.
    /// </summary>
    void UpdateDependencyResults(SubTask completedSubTask, string result, List<SubTask> allSubTasks);

    /// <summary>
    /// Check for newly available tasks after dependency completion.
    /// </summary>
    List<SubTask> CheckForNewlyAvailableTasks(List<SubTask> allSubTasks);

    /// <summary>
    /// Update the startability status of all subtasks based on their dependencies.
    /// </summary>
    void UpdateSubTaskStartability(List<SubTask> subTasks);
}

/// <summary>
/// Implementation of dependency management logic.
/// Based on the working dependency management from /src/Services/OrchestratorStateMachine.cs
/// </summary>
public class DependencyManager : IDependencyManager
{
    private readonly ILogger<DependencyManager> _logger;

    public DependencyManager(ILogger<DependencyManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Update dependency results when a subtask completes successfully.
    /// This allows dependent subtasks to access the results they need.
    /// </summary>
    public void UpdateDependencyResults(SubTask completedSubTask, string result, List<SubTask> allSubTasks)
    {
        _logger.LogDebug("Updating dependency results for completed subtask {SubTaskId}", completedSubTask.SubTaskId);

        // Find all subtasks that depend on this completed subtask
        var dependentSubTasks = allSubTasks
            .Where(st => st.Dependencies.Contains(completedSubTask.SubTaskId))
            .ToList();

        foreach (var dependentSubTask in dependentSubTasks)
        {
            // Add the result to the dependent subtask's dependency results
            dependentSubTask.DependencyResults[completedSubTask.SubTaskId] = result;
            
            _logger.LogDebug("Added dependency result from {CompletedId} to {DependentId}", 
                completedSubTask.SubTaskId, dependentSubTask.SubTaskId);
        }

        // Update startability for all subtasks
        UpdateSubTaskStartability(allSubTasks);

        _logger.LogInformation("Updated dependency results for {DependentCount} subtasks", dependentSubTasks.Count);
    }

    /// <summary>
    /// Check for subtasks that have become available after dependency completion.
    /// Returns subtasks that can now start but haven't been delegated yet.
    /// </summary>
    public List<SubTask> CheckForNewlyAvailableTasks(List<SubTask> allSubTasks)
    {
        var newlyAvailable = allSubTasks
            .Where(st => st.Status == SubTaskStatus.Pending && st.CanStart)
            .ToList();

        _logger.LogDebug("Found {NewlyAvailableCount} newly available tasks", newlyAvailable.Count);

        return newlyAvailable;
    }

    /// <summary>
    /// Update the CanStart status for all subtasks based on their dependencies.
    /// A subtask can start if all its dependencies have been completed successfully.
    /// </summary>
    public void UpdateSubTaskStartability(List<SubTask> subTasks)
    {
        foreach (var subTask in subTasks)
        {
            if (subTask.Status != SubTaskStatus.Pending)
            {
                // Only pending tasks need startability updates
                continue;
            }

            // Check if all dependencies are satisfied
            var canStart = true;
            
            foreach (var dependencyId in subTask.Dependencies)
            {
                // Find the dependency subtask
                var dependencySubTask = subTasks.FirstOrDefault(st => st.SubTaskId == dependencyId);
                
                if (dependencySubTask == null)
                {
                    _logger.LogWarning("Dependency {DependencyId} not found for subtask {SubTaskId}", 
                        dependencyId, subTask.SubTaskId);
                    canStart = false;
                    break;
                }

                // Check if dependency is completed successfully
                if (dependencySubTask.Status != SubTaskStatus.Completed)
                {
                    canStart = false;
                    break;
                }

                // Check if we have the dependency result
                if (!subTask.DependencyResults.ContainsKey(dependencyId))
                {
                    canStart = false;
                    break;
                }
            }

            var previousCanStart = subTask.CanStart;
            subTask.CanStart = canStart;

            if (previousCanStart != canStart)
            {
                _logger.LogDebug("Subtask {SubTaskId} startability changed from {Previous} to {Current}", 
                    subTask.SubTaskId, previousCanStart, canStart);
            }
        }
    }
} 