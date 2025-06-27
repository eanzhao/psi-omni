using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Models;
using Aevatar.Core.Abstractions;

namespace PsiAgent;

public partial class PsiGAgent
{
    private async Task<List<CallbackData>> DelegateStartableSubTasksAsync()
    {
        var callbackDatas = new List<CallbackData>();
        var profiles = CollectAgentProfiles();

        foreach (var subTask in State.Orchestrator.CurrentSubTasks.Where(st =>
                     st is { CanStart: true, Status: SubTaskStatus.Pending }))
        {
            var callbackData = await DelegateTaskAsync(subTask, profiles);
            callbackDatas.Add(callbackData);
        }

        return callbackDatas;
    }

    private string? GetAvailableChildAgentId()
    {
        // Collect all child agent IDs assigned to subtasks
        var allAssigned = State.Orchestrator.CurrentSubTasks
            .Where(st => !string.IsNullOrEmpty(st.ChildAgentId))
            .Select(st => st.ChildAgentId)
            .Distinct()
            .ToList();

        // Find agents that are not currently assigned to any Pending/Delegated subtask
        var busyAgents = State.Orchestrator.CurrentSubTasks
            .Where(st => (st.Status == SubTaskStatus.Pending || st.Status == SubTaskStatus.Delegated) &&
                         !string.IsNullOrEmpty(st.ChildAgentId))
            .Select(st => st.ChildAgentId)
            .Distinct()
            .ToHashSet();

        var idleAgents = allAssigned.Where(id => !busyAgents.Contains(id)).ToList();
        return idleAgents.FirstOrDefault();
    }

    private class ToolInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private class AgentProfile
    {
        public string AgentId { get; set; } = string.Empty;
        public string LastTask { get; set; } = string.Empty;
        public List<ToolInfo> RequiredTools { get; set; } = new();
        public AgentRole Role { get; set; }
        public string? ModelId { get; set; }
        public string? LastReframedTask { get; set; }
    }

    private List<AgentProfile> CollectAgentProfiles()
    {
        var profiles = new List<AgentProfile>();
        var registry = _kernelFactory.FunctionRegistry;
        var grouped = State.Orchestrator.CurrentSubTasks
            .Where(st => !string.IsNullOrEmpty(st.ChildAgentId) && st.Status != SubTaskStatus.Pending)
            .GroupBy(st => st.ChildAgentId!);
        foreach (var group in grouped)
        {
            var lastTask = group.OrderByDescending(st => st.Status == SubTaskStatus.Completed ? 1 : 0).First();
            var toolInfos = new List<ToolInfo>();
            if (registry != null)
            {
                foreach (var toolName in lastTask.RequiredTools)
                {
                    var func = registry.GetToolByQualifiedName(toolName);
                    var desc = func?.Description ?? "";
                    toolInfos.Add(new ToolInfo { Name = toolName, Description = desc });
                }
            }
            else
            {
                foreach (var toolName in lastTask.RequiredTools)
                {
                    toolInfos.Add(new ToolInfo { Name = toolName, Description = "" });
                }
            }

            profiles.Add(new AgentProfile
            {
                AgentId = group.Key!,
                LastTask = lastTask.Task,
                RequiredTools = toolInfos,
                Role = lastTask.SuggestedRole,
                ModelId = State.Configuration?.Model?.ModelId,
                LastReframedTask = lastTask.ReframedTask
            });
        }

        return profiles;
    }

    private string BuildAgentSelectionPrompt(List<AgentProfile> profiles, SubTask subTask)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "You are an expert agent orchestrator. Given the following existing child agents and their capabilities, decide which agent (if any) is best suited to handle the new subtask. If none are suitable, recommend creating a new agent.");
        sb.AppendLine();
        sb.AppendLine("Existing child agents:");
        foreach (var profile in profiles)
        {
            sb.AppendLine($"- AgentId: {profile.AgentId}");
            sb.AppendLine($"  LastTask: {profile.LastTask}");
            sb.AppendLine($"  LastReframedTask: {profile.LastReframedTask}");
            sb.AppendLine("  RequiredTools:");
            foreach (var tool in profile.RequiredTools)
            {
                sb.AppendLine($"    - Name: {tool.Name}, Description: {tool.Description}");
            }

            sb.AppendLine($"  Role: {profile.Role}");
            sb.AppendLine($"  ModelId: {profile.ModelId}");
            sb.AppendLine();
        }

        sb.AppendLine("New subtask to assign:");
        sb.AppendLine($"- Task: {subTask.Task}");
        sb.AppendLine($"- RequiredTools: {string.Join(", ", subTask.RequiredTools)}");
        sb.AppendLine($"- ReframedTask: <reframed_task>{subTask.ReframedTask}</reframed_task>");
        sb.AppendLine();
        sb.AppendLine(
            "Respond in JSON: {\"reuse_agent_id\": \"<AgentId or null>\", \"reasoning\": \"your reasoning\"}");
        return sb.ToString();
    }

    private async Task<string?> LLM_SelectAgentAsync(string prompt)
    {
        var agentConfig = State.Configuration;
        var kernel = _kernelFactory.CreateKernel(agentConfig);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var result = await chatService.GetChatMessageContentAsync(prompt);
        var content = result.Content ?? "";
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart != -1 && jsonEnd != -1)
            {
                content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("reuse_agent_id", out var idProp))
            {
                var id = idProp.GetString();
                return string.IsNullOrEmpty(id) || id == "null" ? null : id;
            }
        }
        catch
        {
            /* fallback: no reuse */
        }

        return null;
    }

    private async Task<CallbackData> DelegateTaskAsync(SubTask subTask, List<AgentProfile> profiles)
    {
        var callId = subTask.SubTaskId;
        var recommendedAgentId = "";
        if (profiles.Count > 0)
        {
            var prompt = BuildAgentSelectionPrompt(profiles, subTask);
            recommendedAgentId = await LLM_SelectAgentAsync(prompt);
        }

        IGAgent child;
        if (!string.IsNullOrEmpty(recommendedAgentId))
        {
            // Reuse existing agent
            child = await _gAgentFactory.GetGAgentAsync(Orleans.Runtime.GrainId.Parse(recommendedAgentId));
            Logger.LogInformation($"LLM recommends reusing child agent {recommendedAgentId} for subtask {callId}");
            var taskDescription = await PrepareTaskWithDependencyContextAsync(subTask);
            var targetAgentId = child.GetGrainId();
            await PublishAsync(targetAgentId, new ContinueConversationEvent
            {
                CallId = callId,
                TargetAgentId = targetAgentId.ToString(),
                UserMessage = taskDescription
            });
            // Update subTask's ChildAgentId
            subTask.ChildAgentId = child.GetGrainId().ToString();
            return new CallbackData
            {
                CallId = callId,
                ChildAgentId = child.GetGrainId().ToString(),
                Task = subTask.Task,
                CreatedAt = DateTime.UtcNow
            };
        }
        else
        {
            // Create new agent
            child = await _gAgentFactory.GetGAgentAsync("psi", "psi");
            Logger.LogInformation($"LLM recommends creating new child agent for subtask {callId}");
            await RegisterAsync(child);
            var config = new AgentConfiguration()
            {
                Model = State.Configuration.Model
            };
            await PublishAsync(child.GetGrainId(), new SendConfigEvent
            {
                Configuration = config,
                ParenteAgentId = State.AgentId
            });
            var taskDescription = await PrepareTaskWithDependencyContextAsync(subTask);
            await PublishAsync(child.GetGrainId(), new SendTaskEvent
            {
                CallId = callId,
                Task = taskDescription
            });
            // Update subTask's ChildAgentId
            subTask.ChildAgentId = child.GetGrainId().ToString();
            return new CallbackData
            {
                CallId = callId,
                ChildAgentId = child.GetGrainId().ToString(),
                Task = subTask.Task,
                CreatedAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Prepare task with dependency context by using LLM to intelligently frame the subtask
    /// with relevant information from completed dependencies.
    /// </summary>
    private async Task<string> PrepareTaskWithDependencyContextAsync(SubTask subTask)
    {
        if (subTask.DependencyResults.Count == 0)
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
            // TODO: Flatten dependency tree
            foreach (var subTaskDependencyResult in subTask.DependencyResults)
            {
                var dep = subTaskDependencyResult.Key;
                var res = subTaskDependencyResult.Value;
                var depTask = State.Orchestrator.CurrentSubTasks.SingleOrDefault(st => st.SubTaskId == dep);
                var depTaskDesc = "";
                if (depTask != null)
                {
                    depTaskDesc = depTask.ReframedTask.IsNullOrEmpty() ? depTask.Task : depTask.ReframedTask;
                }

                dependencyInfo.AppendLine(
                    $"<dependency><id>{dep}</id><task>{depTaskDesc}</task><result>{res}</result></dependency>");
                dependencyInfo.AppendLine();
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
            subTask.ReframedTask = reframedTask;

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