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
        await PublishAsync(child.GetGrainId(), new SendConfigEvent
        {
            Configuration = config,
            ParenteAgentId = State.AgentId
        });
        await PublishAsync(child.GetGrainId(), new SendTaskEvent
        {
            CallId = callId,
            Task = subTask.Task
        });
        return new CallbackData
        {
            CallId = callId,
            ChildAgentId = child.GetGrainId().ToString(),
            Task = subTask.Task,
            CreatedAt = DateTime.UtcNow
        };
    }
}