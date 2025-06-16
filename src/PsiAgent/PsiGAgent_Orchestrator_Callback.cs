using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using PsiOrleans.Common.Models;

namespace PsiAgent;

public partial class PsiGAgent
{
    private void UpdateDependencyResults(List<SubTask> allSubTasks, SubTask subTask, string result)
    {
        Logger.LogDebug("Updating dependency results for completed subtask {SubTaskId}", subTask.SubTaskId);

        // Find all subtasks that depend on this completed subtask
        var dependentSubTasks = allSubTasks
            .Where(st => st.Dependencies.Contains(subTask.SubTaskId))
            .ToList();

        foreach (var dependentSubTask in dependentSubTasks)
        {
            dependentSubTask.Dependencies.Remove(subTask.SubTaskId);
            // Add the result to the dependent subtask's dependency results
            dependentSubTask.DependencyResults[subTask.SubTaskId] = result;

            Logger.LogDebug("Added dependency result from {CompletedId} to {DependentId}",
                subTask.SubTaskId, dependentSubTask.SubTaskId);
        }

        foreach (var st in allSubTasks)
        {
            // A subtask can start if it has no dependencies
            st.CanStart = !st.Dependencies.Any();
        }

        Logger.LogInformation("Updated dependency results for {DependentCount} subtasks", dependentSubTasks.Count);
    }
}