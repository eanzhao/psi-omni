using System.Text.Json;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PsiOrleans.Common;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;

namespace PsiAgnet.Specialized;

[Serializable]
[GenerateSerializer]
public class PsiSpecializedGAgentState : StateBase
{
    /// <summary>
    /// Chat history for this agent
    /// </summary>
    [Id(0)]
    public List<ChatMessage> ChatHistory { get; set; } = new();

    [Id(1)] public string AgentId { get; set; } = string.Empty;
    [Id(2)] public string UserAgentId { get; set; } = string.Empty;
    [Id(3)] public string CallId { get; set; } = string.Empty;
    [Id(4)] public AgentConfiguration? Configuration { get; set; }
    [Id(5)] public List<string> Tools { get; set; } = new();
    [Id(6)] public string SystemPrompt { get; set; } = string.Empty;
}

[GenerateSerializer]
public class PsiSpecializedGAgentStateLogEvent : StateLogEventBase<PsiSpecializedGAgentStateLogEvent>;

[GenerateSerializer]
public class UpdateSendConfigEvent : PsiSpecializedGAgentStateLogEvent
{
    [Id(0)] public AgentConfigEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class ReceiveUserMessageEvent : PsiSpecializedGAgentStateLogEvent
{
    [Id(0)] public UserMessageEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class GrowChatHistoryEvent : PsiSpecializedGAgentStateLogEvent
{
    [Id(0)] public List<ChatMessage> NewMessages { get; set; } = new();
}

[GAgent("psi", "specialized")]
public partial class PsiSpecializedGAgent : GAgentBase<PsiSpecializedGAgentState, PsiSpecializedGAgentStateLogEvent>
{
    private readonly IKernelFactory _kernelFactory;
    private readonly IGAgentFactory _gAgentFactory;

    public PsiSpecializedGAgent(
        IKernelFactory kernelFactory,
        IGAgentFactory gAgentFactory
    )
    {
        _kernelFactory = kernelFactory;
        _gAgentFactory = gAgentFactory;
    }

    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult($"System Prompt:\n{State.SystemPrompt}\n\nTools:\n{string.Join("\n", State.Tools)}");
    }

    [EventHandler]
    public async Task HandleSendConfigEventAsync(AgentConfigEvent @event)
    {
        Logger.LogInformation("SendConfigEvent: {Task}", @event.Configuration.Model.ModelId);
        RaiseEvent(new UpdateSendConfigEvent()
        {
            Event = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleUserMessageEventAsync(UserMessageEvent @event)
    {
        if (@event.TargetAgentId != this.GetGrainId().ToString())
        {
            // Not for me
            return;
        }
        RaiseEvent(new ReceiveUserMessageEvent()
        {
            Event = @event
        });
        await ConfirmEvents();
    }

    private async Task RunAsync()
    {
        if (State.ChatHistory.IsNullOrEmpty())
        {
            // Do nothing
            return;
        }

        if (State.Configuration == null)
        {
            // Do nothing
            return;
        }

        var kernel = _kernelFactory.CreateKernel(
            State.Configuration,
            State.Tools
        );
        if (kernel == null)
            throw new InvalidOperationException("Kernel is not configured for tool execution.");

        // 1. 获取 chat completion 服务
        var chatService = kernel.GetRequiredService<IChatCompletionService>();


        // 2. 构造 PromptExecutionSettings
        var maxTokens = 4000; // 默认最大 token
        var temperature = 0.1; // 默认温度
        // 只用 OpenAI 版本（无 config.Model 判断）
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = maxTokens,
            Temperature = temperature
        };

        var chatHistory = GetChatHistory();
        var preChatHistoryLength = chatHistory.Count;
        var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
        chatHistory.Add(result);
        var newMessages = chatHistory.Skip(preChatHistoryLength).Select(m =>
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

                if (m.Role == AuthorRole.Tool)
                {
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
        RaiseEvent(new GrowChatHistoryEvent()
        {
            NewMessages = newMessages
        });
        await ConfirmEvents();
    }

    private async Task ReplyAsync()
    {
        await PublishAsync(GrainId.Parse(State.UserAgentId), new AgentMessageEvent
        {
            TargetAgentId = State.UserAgentId,
            CallId = State.CallId,
            Content = State.ChatHistory.Last().Content
        });
    }


    protected override void GAgentTransitionState(PsiSpecializedGAgentState state,
        StateLogEventBase<PsiSpecializedGAgentStateLogEvent> @event)
    {
        switch (@event)
        {
            case UpdateSendConfigEvent payload:
                if (state.AgentId.IsNullOrEmpty())
                {
                    var grainId = this.GetGrainId().ToString();
                    var config = payload.Event.Configuration;
                    state.AgentId = grainId;
                    state.UserAgentId = payload.Event.ParentAgentId;
                    state.Configuration = config;
                    state.Tools = payload.Event.Tools;
                }

                break;
            case ReceiveUserMessageEvent payload:

                if (!payload.Event.CallId.IsNullOrEmpty())
                {
                    state.CallId = payload.Event.CallId;
                }

                if (!payload.Event.Content.IsNullOrEmpty())
                {
                    var message = ChatMessage.CreateUserMessage(payload.Event.Content);
                    message.Metadata["CallId"] = payload.Event.CallId;
                    state.ChatHistory.Add(message);
                    DoAsync(RunAsync);
                }

                break;
            case GrowChatHistoryEvent payload:
                state.ChatHistory.AddRange(payload.NewMessages);
                DoAsync(ReplyAsync);
                break;
        }
    }

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

    private ChatHistory GetChatHistory()
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(State.SystemPrompt);
        var messages =
            State.ChatHistory.Select(message => ChatMessageConverter.ToSemanticKernelMessage(message));
        chatHistory.AddRange(messages);

        return chatHistory;
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