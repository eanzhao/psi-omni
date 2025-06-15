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
    public async Task HandleSendConfigEventAsync(SendConfigEvent @event)
    {
        RaiseEvent(new UpdateSendConfigEvent()
        {
            SendConfigEvent = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleSendTaskEventAsync(SendTaskEvent @event)
    {
        var analysis = await _taskAnalyzer.AnalyzeTaskAsync(State);
        if (analysis.RecommendedApproach == TaskApproach.DirectExecution)
        {
        }
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
            case UpdateTaskAnalysicResultEvent payload:
                if (state.TaskAnalysisResult.RecommendedApproach == TaskApproach.Unknown)
                {
                    state.TaskAnalysisResult = payload.TaskAnalysisResult;
                    AsyncHelper.RunSync(async () =>
                    {
                        var grainId = this.GetGrainId();
                        await PublishAsync(grainId, new TaskAnalysisDone());
                    });
                }

                break;
        }
        base.GAgentTransitionState(state, @event);
    }
    
    private async Task PublishAsync<T>(GrainId grainId, T @event) where T : EventBase
    {   
        var grainIdString = grainId.ToString();
        var streamId = StreamId.Create(AevatarOptions!.StreamNamespace, grainIdString);
        var stream = StreamProvider.GetStream<EventWrapperBase>(streamId);
        var eventWrapper = new EventWrapper<T>(@event, Guid.NewGuid(), this.GetGrainId());
        await stream.OnNextAsync(eventWrapper);
    }
}