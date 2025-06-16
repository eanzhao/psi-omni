using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;
using PsiOrleans.Specialized.Services;
using Volo.Abp.Threading;

namespace PsiAgent;

[GAgent("psi", "psi")]
public partial class PsiGAgent : GAgentBase<AgentState, AgentStateLogEvent>
{
    private readonly IKernelFactory _kernelFactory;
    private readonly ITaskAnalyzer _taskAnalyzer;
    private readonly IGAgentFactory _gAgentFactory;

    public PsiGAgent(
        IKernelFactory kernelFactory,
        ITaskAnalyzer taskAnalyzer,
        IGAgentFactory gAgentFactory
    )
    {
        _kernelFactory = kernelFactory;
        _taskAnalyzer = taskAnalyzer;
        _gAgentFactory = gAgentFactory;
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a Psi autonomous agent.");
    }

    [EventHandler]
    public async Task HandlePingEventAsync(PingEvent @event)
    {
        Logger.LogInformation("Pingevent");
    }

    [EventHandler]
    public async Task HandleTaskAnalysisDoneEventAsync(TaskAnalysisDone @event)
    {
        Logger.LogInformation("Task Analysis Done");
        if (State.TaskAnalysisResult.RecommendedApproach == TaskApproach.DirectExecution)
        {
            var chatHistory = await ExecuteSpecializedAsync();
            if (chatHistory != null)
            {
                var serializable = chatHistory.Select(m => new ChatMessage(m.Role.ToString(), m.Content))
                    .ToList();
                RaiseEvent(new UpdateSpecializedRunResultEvent
                {
                    ChatHistory = serializable
                });
                await ConfirmEvents();
            }
        }
        else if (State.TaskAnalysisResult.RecommendedApproach == TaskApproach.Orchestration)
        {
            var subtasks = await DecomposeTaskAsync();
            if (!subtasks.IsNullOrEmpty())
            {
                RaiseEvent(new UpdateSubTasksEvent
                {
                    SubTasks = subtasks
                });
                await ConfirmEvents();
            }
        }
    }

    [EventHandler]
    public async Task HandleSpecializedRunDoneEventAsync(SpecializedRunDone specializedRunDone)
    {
        if (State.ParentAgentId.IsNullOrEmpty())
        {
            return;
        }

        await PublishAsync(GrainId.Parse(State.ParentAgentId), new TaskCallbackEvent
        {
            CallId = State.CallId,
            Task = State.Task,
            Reply = State.SpecializedState.ChatHistory.Last()
        });
        // TODO: maybe send callback to parent.
        Logger.LogInformation("SpecializedRunDone");
    }

    [EventHandler]
    public async Task HandleOrchestratorRunDoneEventAsync(OrchestratorRunDone orchestratorRunDone)
    {
        if (State.ParentAgentId.IsNullOrEmpty())
        {
            Logger.LogInformation("Result for task:\n\nTask: {Task}\n\nResult: {Result}", State.Task, orchestratorRunDone.Reply);
            return;
        }

        await PublishAsync(GrainId.Parse(State.ParentAgentId), new TaskCallbackEvent
        {
            CallId = State.CallId,
            Task = State.Task,
            Reply = ChatMessage.CreateAssistantMessage(orchestratorRunDone.Reply)
        });
        // TODO: maybe send callback to parent.
        Logger.LogInformation("SpecializedRunDone");
    }

    [EventHandler]
    public async Task HandleSendConfigEventAsync(SendConfigEvent @event)
    {
        Logger.LogInformation("SendConfigEvent: {Task}", @event.Configuration.Model.ModelId);
        RaiseEvent(new UpdateSendConfigEvent()
        {
            SendConfigEvent = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleSendTaskEventAsync(SendTaskEvent @event)
    {
        Logger.LogInformation("SendTaskEvent: {Task}", @event.Task);
        RaiseEvent(new UpdateTaskEvent()
        {
            Task = @event.Task,
            CallId = @event.CallId
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleTaskCallbackEventAsync(TaskCallbackEvent @event)
    {
        RaiseEvent(new ReceiveCallbackEvent
        {
            TaskCallbackEvent = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleTaskSetEventAsync(TaskSet @event)
    {
        Logger.LogInformation("TaskSetEvent: {Task}", State.Task);
        var analysisResult = await _taskAnalyzer.AnalyzeTaskAsync(State);
        RaiseEvent(new UpdateTaskAnalysicResultEvent
        {
            TaskAnalysisResult = analysisResult
        });
        await ConfirmEvents();
    }

    protected override void GAgentTransitionState(AgentState state, StateLogEventBase<AgentStateLogEvent> @event)
    {
        switch (@event)
        {
            case UpdateSendConfigEvent payload:
                if (state.AgentId.IsNullOrEmpty())
                {
                    var grainId = this.GetGrainId().ToString();
                    var config = payload.SendConfigEvent.Configuration;
                    state.AgentId = grainId;
                    state.ParentAgentId = payload.SendConfigEvent.ParenteAgentId;
                    state.Configuration = config;
                    state.AgentRole = AgentRole.Undecided;
                    state.Orchestrator = new OrchestratorState();
                }

                break;
            case UpdateTaskEvent payload:
                if (string.IsNullOrEmpty(state.Task))
                {
                    state.Task = payload.Task;
                    state.CallId = payload.CallId;
                    DoAsync(async () =>
                    {
                        var grainId = this.GetGrainId();
                        await HandleTaskSetEventAsync(new TaskSet());
                    });
                }

                break;
            case UpdateTaskAnalysicResultEvent payload:
                if (state.TaskAnalysisResult.RecommendedApproach == TaskApproach.Unknown)
                {
                    state.TaskAnalysisResult = payload.TaskAnalysisResult;
                    DoAsync(async () =>
                    {
                        var grainId = this.GetGrainId();
                        await HandleTaskAnalysisDoneEventAsync(new TaskAnalysisDone());
                    });
                }

                break;
            case UpdateSpecializedRunResultEvent payload:
                if (state.SpecializedState.ChatHistory.IsNullOrEmpty())
                {
                    state.SpecializedState.ChatHistory.AddRange(payload.ChatHistory);
                    DoAsync(async () =>
                    {
                        var grainId = this.GetGrainId();
                        await HandleSpecializedRunDoneEventAsync(new SpecializedRunDone());
                    });
                }

                break;
            case UpdateSubTasksEvent payload:
                if (state.Orchestrator.CurrentSubTasks.IsNullOrEmpty())
                {
                    state.Orchestrator.CurrentSubTasks.AddRange(payload.SubTasks);
                    state.Orchestrator.ExecutionPlan = string.Join("\n", payload.SubTasks.Select(st => $"- {st.Task}"));
                    state.Orchestrator.CreatedAt = DateTime.UtcNow;
                    state.Orchestrator.LastUpdated = DateTime.UtcNow;

                    DoAsync(async () =>
                    {
                        var callbackDatas = await DelegateStartableSubTasksAsync();
                        RaiseEvent(new UpdateSubTaskCallbackDatasEvent
                        {
                            CallbackDatas = callbackDatas
                        });
                    });
                }

                break;
            case UpdateSubTaskCallbackDatasEvent payload:
                foreach (var callbackData in payload.CallbackDatas)
                {
                    var subTask =
                        state.Orchestrator.CurrentSubTasks.SingleOrDefault(st => st.SubTaskId == callbackData.CallId);
                    if (subTask != null)
                    {
                        // TODO: Assert Status is Pending
                        subTask.Status = SubTaskStatus.Delegated;
                        state.Orchestrator.PendingCallbacks[callbackData.CallId] = callbackData;
                    }
                }

                break;
            case ReceiveCallbackEvent payload:
                var orchestratorState = State.Orchestrator;
                var callback = payload.TaskCallbackEvent;
                if (orchestratorState.PendingCallbacks.TryGetValue(callback.CallId, out var cbd))
                {
                    cbd.IsReceived = true;
                    cbd.ResultMessage = callback.Reply.Content;
                    cbd.IsSuccess = true; // TODO: Get from replay
                    cbd.ReceivedAt = DateTime.UtcNow;
                    orchestratorState.CompletedCallbacks.Add(new CompletedCallback
                    {
                        CallId = callback.CallId,
                        ChildAgentId = cbd.ChildAgentId,
                        Task = cbd.Task,
                        ResultMessage = callback.Reply.Content,
                        IsSuccess = true,
                        CompletedAt = cbd.ReceivedAt ?? DateTime.UtcNow,
                        ExecutionTime = (cbd.ReceivedAt ?? DateTime.UtcNow) - cbd.CreatedAt
                    });
                    orchestratorState.PendingCallbacks.Remove(callback.CallId);
                    var subTask = orchestratorState.CurrentSubTasks.SingleOrDefault(st => st.SubTaskId == cbd.CallId);
                    if (subTask != null)
                    {
                        subTask.Status = SubTaskStatus.Completed;
                        subTask.SubTaskId = callback.CallId;
                        UpdateDependencyResults(orchestratorState.CurrentSubTasks, subTask, callback.Reply.Content);
                    }
                }

                DoAsync(async () =>
                {
                    var decision = await AnalyzeProgressAsync();
                    if (decision == OrchestrationDecision.CompleteTask)
                    {
                        var result = await AggregateResultsAsync();
                        await HandleOrchestratorRunDoneEventAsync(new OrchestratorRunDone()
                        {
                            Reply = result
                        });
                    }
                    else if (decision == OrchestrationDecision.CreateAdditionalTasks)
                    {
                        var callbackDatas = await DelegateStartableSubTasksAsync();
                        RaiseEvent(new UpdateSubTaskCallbackDatasEvent
                        {
                            CallbackDatas = callbackDatas
                        });
                    }
                });

                break;
        }

        base.GAgentTransitionState(state, @event);
    }

    /// <summary>
    /// Schedules a one-time execution of HandleTaskSetEventAsync(new TaskSet()) using Orleans RegisterTimer.
    /// This ensures the event is triggered safely within the Grain context, avoiding thread-safety issues.
    /// </summary>
    [Obsolete("Obsolete")]
    private void DoAsync(Func<Task> action)
    {
        // Orleans RegisterTimer ensures the callback runs in the Grain's context.
        RegisterTimer(
            async _ => await action(),
            state: null,
            dueTime: TimeSpan.Zero, // Trigger immediately
            period: TimeSpan.FromMilliseconds(-1) // Only once
        );
    }

    private async Task PublishAsync<T>(GrainId grainId, T @event) where T : EventBase
    {
        Logger.LogInformation("PublishAsync: {GrainId} {EventName}", grainId, typeof(T).Name);
        var grainIdString = grainId.ToString();
        var streamId = StreamId.Create(AevatarOptions!.StreamNamespace, grainIdString);
        var stream = StreamProvider.GetStream<EventWrapperBase>(streamId);
        var eventWrapper = new EventWrapper<T>(@event, Guid.NewGuid(), this.GetGrainId());
        await stream.OnNextAsync(eventWrapper);
    }
}