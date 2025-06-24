using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;

namespace PsiAgent;

[GAgent("psi", "psi")]
public partial class PsiGAgent : GAgentBase<AgentState, AgentStateLogEvent>
{
    private readonly IKernelFactory _kernelFactory;
    private readonly IGAgentFactory _gAgentFactory;

    public PsiGAgent(
        IKernelFactory kernelFactory,
        IGAgentFactory gAgentFactory
    )
    {
        _kernelFactory = kernelFactory;
        _gAgentFactory = gAgentFactory;
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This is a Psi autonomous agent.");
    }

    [EventHandler]
    public async Task HandlePingEventAsync(PingEvent @event)
    {
        Logger.LogInformation($"{this.GetGrainId()} Pingevent");
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
        if (@event.TargetAgentId != this.GetGrainId().ToString())
        {
            // Not for me
            return;
        }

        RaiseEvent(new ReceiveCallbackEvent
        {
            TaskCallbackEvent = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleContinueConversationEventAsync(ContinueConversationEvent @event)
    {
        if (!State.Orchestrator.IsInConversation) return;

        Logger.LogInformation("ContinueConversationEvent: {Message}", @event.UserMessage);

        RaiseEvent(new UpdateOrchestratorChatEvent
        {
            Messages = new List<ChatMessage> { ChatMessage.CreateUserMessage(@event.UserMessage) }
        });

        DoAsync(async () =>
        {
            // TODO: Add a follow up chat handler instead of doing this.
            var (decision, newSubTasks, reply) = await DecideNextOrchestratorActionAsync();
            if (decision == OrchestrationDecision.CompleteTask)
            {
                await CompleteOrchestratorExecutionAsync(reply, isFinal: true);
            }
            else if (decision == OrchestrationDecision.ContinueConversation)
            {
                await CompleteOrchestratorExecutionAsync(reply, isFinal: false);
            }
            else if (decision == OrchestrationDecision.CreateAdditionalTasks)
            {
                if (newSubTasks.Any())
                {
                    RaiseEvent(new UpdateSubTasksEvent() { SubTasks = newSubTasks });
                }

                var callbackDatas = await DelegateStartableSubTasksAsync();
                if (callbackDatas.Any())
                {
                    RaiseEvent(new UpdateSubTaskCallbackDatasEvent { CallbackDatas = callbackDatas });
                }
            }
        });
        await ConfirmEvents();
    }

    private async Task StartSpecializedExecutionAsync()
    {
        if (State.TaskAnalysisResult.RecommendedApproach == TaskApproach.DirectExecution)
        {
            try
            {
                var chatHistory = await ExecuteSpecializedAsync();
                if (chatHistory != null)
                {
                    var serializable = chatHistory.Select(m =>
                        {
                            if (m is OpenAIChatMessageContent mm)
                            {
                                var toolCalls = mm.ToolCalls.Select(x => new ToolCall()
                                {
                                    FunctionName = x.FunctionName,
                                    FunctionArguments = x.FunctionArguments.ToString()
                                }).ToList();
                                return new ChatMessage(m.Role.ToString(), m.Content, m.AuthorName ?? string.Empty,
                                    toolCalls);
                            }

                            return new ChatMessage(m.Role.ToString(), m.Content);
                        })
                        .ToList();
                    RaiseEvent(new UpdateSpecializedRunResultEvent
                    {
                        ChatHistory = serializable
                    });
                    await ConfirmEvents();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex, "Error executing specialized task");
            }
        }
    }

    private async Task StartOrchestratorExecutionAsync()
    {
        if (State.TaskAnalysisResult.RecommendedApproach == TaskApproach.Orchestration)
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

    private async Task CompleteSpecializedExecutionAsync()
    {
        if (State.ParentAgentId.IsNullOrEmpty())
        {
            Logger.LogInformation("Result for task:\n\nTask: {Task}\n\nResult: {Result}", State.Task,
                State.SpecializedState.ChatHistory.Last()?.Content);
            return;
        }

        await PublishAsync(GrainId.Parse(State.ParentAgentId), new TaskCallbackEvent
        {
            TargetAgentId = State.ParentAgentId,
            CallId = State.CallId,
            Task = State.Task,
            Reply = State.SpecializedState.ChatHistory.Last()
        });
        // TODO: maybe send callback to parent.
        Logger.LogInformation("SpecializedRunDone");
    }

    private async Task CompleteOrchestratorExecutionAsync(string reply, bool isFinal = true)
    {
        RaiseEvent(new UpdateOrchestratorChatEvent
        {
            Messages = new List<ChatMessage> { ChatMessage.CreateAssistantMessage(reply) }
        });

        if (!isFinal)
        {
            RaiseEvent(new UpdateConversationStatusEvent { IsInConversation = true });
        }

        if (State.ParentAgentId.IsNullOrEmpty())
        {
            Logger.LogInformation("Result for task:\n\nTask: {Task}\n\nResult: {Result}", State.Task, reply);
            return;
        }

        await PublishAsync(GrainId.Parse(State.ParentAgentId), new TaskCallbackEvent
        {
            TargetAgentId = State.ParentAgentId,
            CallId = State.CallId,
            Task = State.Task,
            Reply = ChatMessage.CreateAssistantMessage(reply),
            IsFinal = isFinal
        });

        Logger.LogInformation("Orchestrator execution turn complete. Final: {IsFinal}", isFinal);
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
                        var analysisResult = await AnalyzeTaskAsync(State);
                        RaiseEvent(new UpdateTaskAnalysicResultEvent
                        {
                            TaskAnalysisResult = analysisResult
                        });
                        await ConfirmEvents();
                    });
                }

                break;
            case UpdateTaskAnalysicResultEvent payload:
                if (state.TaskAnalysisResult.RecommendedApproach == TaskApproach.Unknown)
                {
                    state.TaskAnalysisResult = payload.TaskAnalysisResult;
                    if (payload.TaskAnalysisResult.RecommendedApproach == TaskApproach.DirectExecution)
                    {
                        DoAsync(async () => { await StartSpecializedExecutionAsync(); });
                    }
                    else if (payload.TaskAnalysisResult.RecommendedApproach == TaskApproach.Orchestration)
                    {
                        DoAsync(async () => { await StartOrchestratorExecutionAsync(); });
                    }
                }

                break;
            case UpdateSpecializedRunResultEvent payload:
                if (state.SpecializedState.ChatHistory.IsNullOrEmpty())
                {
                    state.SpecializedState.ChatHistory.AddRange(payload.ChatHistory);
                    DoAsync(async () => { await CompleteSpecializedExecutionAsync(); });
                }

                break;
            case UpdateSubTasksEvent payload:
                if (payload.SubTasks.Count == 0) break;
                if (state.Orchestrator.CurrentSubTasks.Count == 0)
                    state.Orchestrator.CreatedAt = DateTime.UtcNow;
                state.Orchestrator.LastUpdated = DateTime.UtcNow;
                state.Orchestrator.CurrentSubTasks.AddRange(payload.SubTasks);
                state.Orchestrator.ExecutionPlan = string.Join("\n", payload.SubTasks.Select(st => $"- {st.Task}"));

                DoAsync(async () =>
                {
                    var callbackDatas = await DelegateStartableSubTasksAsync();
                    RaiseEvent(new UpdateSubTaskCallbackDatasEvent
                    {
                        CallbackDatas = callbackDatas
                    });
                });

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

                state.Orchestrator.ConversationHistory.AddRange(
                    payload.CallbackDatas.Select(cbd =>
                        ChatMessage.CreateSystemMessage(
                            $"Delegated subtask: {cbd.Task} to agent {cbd.ChildAgentId}"))
                );

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

                state.Orchestrator.ConversationHistory.Add(
                    ChatMessage.CreateSystemMessage(
                        $"Received result from subtask {callback.CallId}: {callback.Reply.Content}")
                );

                DoAsync(async () =>
                {
                    var (decision, newSubTasks, reply) = await DecideNextOrchestratorActionAsync();
                    if (decision == OrchestrationDecision.CompleteTask)
                    {
                        var result = await AggregateResultsAsync();
                        await CompleteOrchestratorExecutionAsync(result, isFinal: true);
                    }
                    else if (decision == OrchestrationDecision.ContinueConversation)
                    {
                        await CompleteOrchestratorExecutionAsync(reply, isFinal: false);
                    }
                    else if (decision == OrchestrationDecision.CreateAdditionalTasks)
                    {
                        if (newSubTasks.Any())
                        {
                            RaiseEvent(new UpdateSubTasksEvent() { SubTasks = newSubTasks });
                        }

                        var callbackDatas = await DelegateStartableSubTasksAsync();
                        if (callbackDatas.Any())
                        {
                            RaiseEvent(new UpdateSubTaskCallbackDatasEvent { CallbackDatas = callbackDatas });
                        }
                    }

                    await ConfirmEvents();
                });

                break;
            case UpdateOrchestratorChatEvent payload:
                if (payload.Messages.Any())
                {
                    state.Orchestrator.ConversationHistory.AddRange(payload.Messages);
                }

                break;
            case UpdateConversationStatusEvent payload:
                state.Orchestrator.IsInConversation = payload.IsInConversation;
                break;
        }

        base.GAgentTransitionState(state, @event);
    }

    /// <summary>
    /// Schedules a one-time execution of HandleTaskSetEventAsync(new TaskSet()) using Orleans RegisterTimer.
    /// This ensures the event is triggered safely within the Grain context, avoiding thread-safety issues.
    /// </summary>
    private void DoAsync(Func<Task> action)
    {
        // Orleans RegisterTimer ensures the callback runs in the Grain's context.
        this.RegisterGrainTimer(action, new GrainTimerCreationOptions
        {
            DueTime = TimeSpan.Zero, // Trigger immediately
            Period = TimeSpan.FromMilliseconds(-1), // Only once
            Interleave = false,
            KeepAlive = false
        });
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