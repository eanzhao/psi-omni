using System.Text.Json;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
        if (@event.TargetAgentId != this.GetGrainId().ToString())
        {
            // Not for me
            return;
        }
        if (State.TaskAnalysisResult.RecommendedApproach == TaskApproach.Orchestration)
        {
            if (!State.Orchestrator.IsInConversation) return;

            Logger.LogInformation("ContinueConversationEvent: {Message}", @event.UserMessage);

            RaiseEvent(new UpdateOrchestratorChatEvent
            {
                Messages = new List<ChatMessage> { ChatMessage.CreateUserMessage(@event.UserMessage) }
            });

            await ConfirmEvents();            
        }
        else if (State.TaskAnalysisResult.RecommendedApproach == TaskApproach.DirectExecution)
        {
         RaiseEvent(new UpdateSpecializedChatEvent()
         {
             CallId = @event.CallId,
             Messages = new List<ChatMessage>()
             {
                 ChatMessage.CreateUserMessage(@event.UserMessage)
             }
         });   
        }
    }

    private async Task StartSpecializedExecutionAsync()
    {
        if (State.TaskAnalysisResult.RecommendedApproach == TaskApproach.DirectExecution)
        {
            try
            {
                var preChatHistoryLength = State.SpecializedState.ChatHistory.Count;
                var chatHistory = await ExecuteSpecializedAsync();
                if (chatHistory.Count > preChatHistoryLength)
                {
                    var serializable = chatHistory.Skip(preChatHistoryLength).Select(m =>
                        {
                            SerializedChatMessageContent serialized = null;
                            if (m is OpenAIChatMessageContent mm)
                            {
                                var json = JsonSerializer.Serialize(mm);
                                serialized = new SerializedChatMessageContent()
                                {
                                    TypeFullName = typeof(OpenAIChatMessageContent).FullName,
                                    Json = json
                                };
                            }
                            if(m.Role == AuthorRole.Tool){
                                var message = new ChatMessage(m.Role.ToString(), m.Content);
                                var functionResult = m.Items.OfType<FunctionResultContent>().FirstOrDefault();
                                if (functionResult != null)
                                {
                                    message.Metadata[OpenAIChatMessageContent.ToolIdProperty] = functionResult.CallId;
                                }

                                return message;
                            }

                            return new ChatMessage(m.Role.ToString(), m.Content)
                            {
                                Serialized = serialized
                            };
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
            Messages = new List<ChatMessage>
            {
                ChatMessage.CreateAssistantMessage(reply),
            },
            IsFinal = isFinal
        });
        //
        // if (!isFinal)
        // {
        //     RaiseEvent(new UpdateConversationStatusEvent { IsInConversation = true });
        // }

        if (State.ParentAgentId.IsNullOrEmpty())
        {
            Logger.LogInformation("Result for task:\n\nTask: {Task}\n\nResult: {Result}", State.Task, reply);
            return;
        }

        DoAsync(async () =>
        {
            RaiseEvent(new TaskCompletedStateLogEvent());
            await ConfirmEvents();
        });
        
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
            case UpdateSpecializedChatEvent payload:
                if (payload.Messages.Any())
                {
                    state.Task = payload.Messages.Last().Content;
                    state.CallId = payload.CallId;
                    state.SpecializedState.ChatHistory.AddRange(payload.Messages);
                    DoAsync(StartSpecializedExecutionAsync);
                }
                break;
            case UpdateTaskAnalysicResultEvent payload:
                if (state.TaskAnalysisResult.RecommendedApproach == TaskApproach.Unknown)
                {
                    state.TaskAnalysisResult = payload.TaskAnalysisResult;
                    if (payload.TaskAnalysisResult.RecommendedApproach == TaskApproach.DirectExecution)
                    {
                        var systemPrompt = "You are a specialized agent. Use the available tool functions. When you are done, summarize the result but do no more tool calls.";
                        state.SpecializedState.ChatHistory.Add(ChatMessage.CreateSystemMessage(systemPrompt));
                        state.SpecializedState.ChatHistory.Add(ChatMessage.CreateUserMessage(State.Task));
                        DoAsync(async () => { await StartSpecializedExecutionAsync(); });
                    }
                    else if (payload.TaskAnalysisResult.RecommendedApproach == TaskApproach.Orchestration)
                    {
                        DoAsync(async () => { await StartOrchestratorExecutionAsync(); });
                    }
                }

                break;
            case UpdateSpecializedRunResultEvent payload:
                if (payload.ChatHistory.Count > 0)
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
                state.Orchestrator.IsInConversation = false;

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
                    {
                        var msg = ChatMessage.CreateAssistantMessage($"Delegated subtask: {cbd.Task} to agent {cbd.ChildAgentId}");
                        msg.ChildInteractions = new List<ChildInteraction> { new ChildInteraction { InteractionType = "TaskAssignment", ChildAgentId = cbd.ChildAgentId, CallId = cbd.CallId, Payload = cbd.Task, Timestamp = DateTime.UtcNow } };
                        return msg;
                    })
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

                var callbackMsg = ChatMessage.CreateAssistantMessage($"Received result from subtask {callback.CallId}: {callback.Reply.Content}");
                callbackMsg.ChildInteractions = new List<ChildInteraction> { new ChildInteraction { InteractionType = "Callback", ChildAgentId = callback.TargetAgentId, CallId = callback.CallId, Payload = callback.Reply.Content, Timestamp = DateTime.UtcNow } };
                state.Orchestrator.ConversationHistory.Add(callbackMsg);

                DoAsync(async () =>
                {
                    await ProgressAsync();
                });

                break;
            case UpdateOrchestratorChatEvent payload:
                if (payload.Messages.Any())
                {
                    state.Orchestrator.ConversationHistory.AddRange(payload.Messages);
                    if (!payload.CallId.IsNullOrEmpty())
                    {
                        state.CallId = payload.CallId;
                        state.Task = payload.Messages.Last().Content;
                    }
                }

                if (payload.Messages.Any() && payload.IsFinal)
                {
                    state.Orchestrator.IsInConversation = true;
                }
                else
                {
                    DoAsync(async () =>
                    {
                        await ProgressAsync();
                    });
                }

                break;
            case TaskCompletedStateLogEvent payload:
                foreach (var subTask in state.Orchestrator.CurrentSubTasks)
                {
                    if (subTask.Status == SubTaskStatus.Pending || subTask.Status == SubTaskStatus.Delegated)
                    {
                        subTask.Status = SubTaskStatus.Canceled;
                    }
                }

                break;
            // case UpdateConversationStatusEvent payload:
            //     state.Orchestrator.IsInConversation = payload.IsInConversation;
            //     break;
        }

        base.GAgentTransitionState(state, @event);
    }

    private async Task ProgressAsync()
    {
        var (decision, newSubTasks, reply, payload) = await DecideNextOrchestratorActionAsync();
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
        else if (decision == OrchestrationDecision.CancelSubTask)
        {
            // Cancel the specified subtask
            var subTaskId = payload?.CancelSubTaskId;
            var subTask = State.Orchestrator.CurrentSubTasks.SingleOrDefault(st => st.SubTaskId == subTaskId);
            if (subTask != null)
            {
                subTask.Status = SubTaskStatus.Failed;
                var cancelMsg = ChatMessage.CreateAssistantMessage($"Canceled subtask: {subTask.Task} (ID: {subTask.SubTaskId})");
                cancelMsg.ChildInteractions = new List<ChildInteraction> { new ChildInteraction { InteractionType = "Cancel", ChildAgentId = subTask.ChildAgentId ?? string.Empty, CallId = subTask.SubTaskId, Payload = "Canceled by orchestrator", Timestamp = DateTime.UtcNow } };
                State.Orchestrator.ConversationHistory.Add(cancelMsg);
                Logger.LogInformation($"Canceled subtask {subTask.SubTaskId}");
            }
        }
        else if (decision == OrchestrationDecision.FollowUpChild)
        {
            // Send follow-up message to the specified child agent
            var childAgentId = payload?.FollowUpChildAgentId;
            var followUpMessage = payload?.FollowUpMessage;
            if (!string.IsNullOrEmpty(childAgentId) && !string.IsNullOrEmpty(followUpMessage))
            {
                var grainId = Orleans.Runtime.GrainId.Parse(childAgentId);
                await PublishAsync(grainId, new ContinueConversationEvent
                {
                    TargetAgentId = grainId.ToString(),
                    UserMessage = followUpMessage
                });
                var followUpMsg = ChatMessage.CreateAssistantMessage($"Sent follow-up to child agent {childAgentId}: {followUpMessage}");
                followUpMsg.ChildInteractions = new List<ChildInteraction> { new ChildInteraction { InteractionType = "FollowUp", ChildAgentId = childAgentId, Payload = followUpMessage, Timestamp = DateTime.UtcNow } };
                State.Orchestrator.ConversationHistory.Add(followUpMsg);
                Logger.LogInformation($"Sent follow-up to child agent {childAgentId}");
            }
        }

        await ConfirmEvents();
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