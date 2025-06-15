using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
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
    private readonly ISpecializedAgentStrategy _specializedAgentStrategy;

    public PsiGAgent(
        IKernelFactory kernelFactory,
        ITaskAnalyzer taskAnalyzer,
        ISpecializedAgentStrategy specializedAgentStrategy
    )
    {
        _kernelFactory = kernelFactory;
        _taskAnalyzer = taskAnalyzer;
        _specializedAgentStrategy = specializedAgentStrategy;
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
                    var agentId = $"agent-{Guid.NewGuid()}";
                    var config = payload.SendConfigEvent.Configuration;
                    state.AgentId = agentId;
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
                        await HandleTaskAnalysisDoneEventAsync( new TaskAnalysisDone());
                    });
                }

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