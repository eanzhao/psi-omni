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

namespace PsiGAgent.Orchestrator;

[Serializable]
[GenerateSerializer]
public class PsiOrchestratorGAgentState : StateBase
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
    [Id(7)] public List<AgentParticulars> ChildAgents { get; set; } = new();
}

[GenerateSerializer]
public class PsiOrchestratorGAgentStateLogEvent : StateLogEventBase<PsiOrchestratorGAgentStateLogEvent>;

[GenerateSerializer]
public class UpdateSendConfigEvent : PsiOrchestratorGAgentStateLogEvent
{
    [Id(0)] public AgentConfigEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class ReceiveUserMessageEvent : PsiOrchestratorGAgentStateLogEvent
{
    [Id(0)] public UserMessageEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class ReceiveAgentMessageEvent : PsiOrchestratorGAgentStateLogEvent
{
    [Id(0)] public AgentMessageEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class GrowChatHistoryEvent : PsiOrchestratorGAgentStateLogEvent
{
    [Id(0)] public List<ChatMessage> NewMessages { get; set; } = new();
}

[GAgent("psi", "orchestrator")]
public class PsiOrchestratorGAgent : GAgentBase<PsiOrchestratorGAgentState, PsiOrchestratorGAgentStateLogEvent>
{
    string SYSTEM_PROMPT = """
                           You are a helpful assistant that can interact with the user, analyze the user's request,
                           break down the request into sub-tasks and create new agents or re-use existing agents to handle the sub-tasks.
                           You can create new agents with the create_agent function.
                           You can list the created agents with the list_created_agents function.
                           You can list the available tools with the list_available_tools function.

                           ## What you are supposed to do
                           - You are the orchestrator of the agents.
                           - You always try to understand the user's request and break it down into sub-tasks.
                           - You are responsible for creating new agents and managing them.
                           - You are responsible for the overall flow of the conversation.
                           - You DON'T use tools other than those for managing agents.

                           
                           
                           ## Rules for creating agents
                           - Agents will use the tools given to them.
                           - Apply separation of concerns. An agent should be responsible for one type of task instead of using tools that are not related by nature.

                           ## Minimize interaction with the user
                           - Don't be verbose and keep asking for confirmation from the user.
                           - Apply your best judgement to create agents without asking for permission.
                           """;

    private readonly IKernelFactory _kernelFactory;
    private readonly IGAgentFactory _gAgentFactory;
    private readonly IAgentService _agentService;
    private readonly HashSet<string> _receivedMessageIds = new HashSet<string>();

    public PsiOrchestratorGAgent(
        IKernelFactory kernelFactory,
        IGAgentFactory gAgentFactory,
        IAgentService agentService
    )
    {
        _kernelFactory = kernelFactory;
        _gAgentFactory = gAgentFactory;
        _agentService = agentService;
    }

    public override Task<string> GetDescriptionAsync()
    {
        throw new NotImplementedException();
    }

    [EventHandler]
    public async Task HandleSendConfigEventAsync(AgentConfigEvent @event)
    {
        _agentService.SetConfiguration(@event.Configuration);
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

        if (_receivedMessageIds.Contains(@event.UniqueId))
        {
            return;
        }

        _receivedMessageIds.Add(@event.UniqueId);
        RaiseEvent(new ReceiveUserMessageEvent()
        {
            Event = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleAgentMessageEventAsync(AgentMessageEvent @event)
    {
        if (@event.TargetAgentId != this.GetGrainId().ToString())
        {
            // Not for me
            return;
        }

        if (_receivedMessageIds.Contains(@event.UniqueId))
        {
            return;
        }

        _receivedMessageIds.Add(@event.UniqueId);

        RaiseEvent(new ReceiveAgentMessageEvent()
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
            State.Configuration
        ); // Orchestrator doesn't have specialized tools.
        if (kernel == null)
            throw new InvalidOperationException("Kernel is not configured for tool execution.");

        kernel.Plugins.AddFromObject(_agentService, "AgentServices");
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
                        message.Metadata["FunctionName"] = functionResult.FunctionName;
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

    protected override void GAgentTransitionState(PsiOrchestratorGAgentState state,
        StateLogEventBase<PsiOrchestratorGAgentStateLogEvent> @event)
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
                    state.SystemPrompt = SYSTEM_PROMPT + $"\nYour agent id is: {grainId}";
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
            case ReceiveAgentMessageEvent payload:
                var amessage = ChatMessage.CreateAssistantMessage(payload.Event.Content);
                amessage.Metadata["CallId"] = payload.Event.CallId;
                state.ChatHistory.Add(amessage);
                DoAsync(RunAsync);
                break;
            case GrowChatHistoryEvent payload:
                state.ChatHistory.AddRange(payload.NewMessages);
                foreach (var message in payload.NewMessages)
                {
                    if (message.Role == "tool" &&
                        message.Metadata.TryGetValue("FunctionName", out var funcNameObject) &&
                        funcNameObject is string funcName && funcName == "create_agent")
                    {
                        try
                        {
                            var agentParticulars = JsonSerializer.Deserialize<AgentParticulars>(message.Content);
                            if (agentParticulars != null && !string.IsNullOrEmpty(agentParticulars.AgentId))
                            {
                                if (!state.ChildAgents.Any(a => a.AgentId == agentParticulars.AgentId))
                                {
                                    state.ChildAgents.Add(agentParticulars);
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // ignore if content is not a valid AgentParticulars json
                        }
                    }
                }
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