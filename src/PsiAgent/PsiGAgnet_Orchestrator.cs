using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Models;

namespace PsiAgent;

public partial class PsiGAgent
{
    private async Task<List<CallbackData>> DelegateStartableSubTasksAsync()
    {
        var callbackDatas = new List<CallbackData>();

        foreach (var subTask in State.Orchestrator.CurrentSubTasks.Where(st =>
                     st is { CanStart: true, Status: SubTaskStatus.Pending }))
        {
            var callbackData = await CreateNewAgentAsync(subTask);
            callbackDatas.Add(callbackData);
        }

        return callbackDatas;
    }

    private async Task<CallbackData> CreateNewAgentAsync(SubTask subTask)
    {
        var callId = subTask.SubTaskId;
        var child = await _gAgentFactory.GetGAgentAsync("psi", "psi");
        await RegisterAsync(child);
        var config = new AgentConfiguration()
        {
            Model = State.Configuration.Model
        };
        var taskDescription = await PrepareTaskWithDependencyContextAsync(subTask);
        await PublishAsync(child.GetGrainId(), new SendConfigEvent
        {
            Configuration = config,
            ParenteAgentId = State.AgentId
        });
        await PublishAsync(child.GetGrainId(), new SendTaskEvent
        {
            CallId = callId,
            Task = taskDescription
        });
        return new CallbackData
        {
            CallId = callId,
            ChildAgentId = child.GetGrainId().ToString(),
            Task = subTask.Task,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Prepare task with dependency context by using LLM to intelligently frame the subtask
    /// with relevant information from completed dependencies.
    /// </summary>
    private async Task<string> PrepareTaskWithDependencyContextAsync(SubTask subTask)
    {
        if (subTask.Dependencies.Count == 0 || subTask.DependencyResults.Count == 0)
        {
            return subTask.Task;
        }

        try
        {
            var agentConfig = State.Configuration;
            var kernel = _kernelFactory.CreateKernel(agentConfig);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            // Gather dependency results
            var dependencyInfo = new StringBuilder();
            foreach (var dependency in subTask.Dependencies)
            {
                if (subTask.DependencyResults.TryGetValue(dependency, out var result))
                {
                    dependencyInfo.AppendLine($"Dependency {dependency} Result: {result}");
                    dependencyInfo.AppendLine();
                }
            }

            var contextPrompt = $@"
You are helping to frame a subtask for an AI agent by intelligently incorporating results from prerequisite tasks.

Original Subtask: {subTask.Task}

Results from completed prerequisite tasks:
{dependencyInfo}

Your task is to:
1. Analyze the prerequisite results and extract information relevant to the current subtask
2. Reframe the original subtask description to be more specific and actionable based on the available data
3. Include any specific values, calculations, or context that the agent will need
4. Create a clear, focused task description that incorporates the prerequisite information

Provide a well-framed task description that includes:
- The original task intent
- Specific data from prerequisites that should be used
- Clear instructions on how to use the prerequisite information
- Any calculations or operations that should be performed with the data

Reframed task description:";

            var llmResponse = await chatService.GetChatMessageContentAsync(contextPrompt);
            var reframedTask = llmResponse.Content ?? subTask.Task;

            Logger.LogInformation("LLM reframed subtask {SubTaskId}: Original='{Original}', Reframed='{Reframed}'",
                subTask.SubTaskId, subTask.Task, reframedTask);

            return reframedTask;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error using LLM to prepare task context for subtask {SubTaskId}, falling back to simple concatenation",
                subTask.SubTaskId);

            // Fallback to the original simple concatenation approach
            return PrepareTaskWithDependencyContextFallback(subTask);
        }
    }

    /// <summary>
    /// Fallback method for simple text concatenation when LLM context preparation fails.
    /// </summary>
    private string PrepareTaskWithDependencyContextFallback(SubTask subTask)
    {
        if (subTask.Dependencies.Count == 0 || subTask.DependencyResults.Count == 0)
        {
            return subTask.Task;
        }

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine($"Task: {subTask.Task}");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Results from prerequisite tasks:");

        foreach (var dependency in subTask.Dependencies)
        {
            if (subTask.DependencyResults.TryGetValue(dependency, out var result))
            {
                contextBuilder.AppendLine($"From subtask {dependency}: {result}");
            }
        }

        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Use the above prerequisite results to complete your task.");

        return contextBuilder.ToString();
    }
}