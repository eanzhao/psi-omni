using System.Text.Json;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PsiAgnet.Omni;
using PsiGAgent.Common;
using PsiGAgent.Common.Interfaces;
using PsiGAgent.Common.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PsiGAgent.Omni;

public enum RealizationStatus
{
    Unrealized,
    Orchestrator,
    Specialized
}

[Serializable]
[GenerateSerializer]
public class PsiOmniGAgentState : StateBase
{
    [Id(0)] public string AgentId { get; set; } = string.Empty;
    [Id(1)] public RealizationStatus RealizationStatus { get; set; } = RealizationStatus.Unrealized;
    [Id(2)] public string SystemPrompt { get; set; } = string.Empty;
    [Id(3)] public List<ToolDefinition> Tools { get; set; } = new();
    [Id(4)] public Dictionary<string, AgentDescriptor> ChildAgents { get; set; } = new();
    [Id(5)] public string Description { get; set; } = string.Empty;
    [Id(6)] public List<AgentExample> Examples { get; set; } = new();
    [Id(7)] public AgentConfiguration? Configuration { get; set; }
    [Id(8)] public string UserAgentId { get; set; } = string.Empty;
    [Id(9)] public string CallId { get; set; } = string.Empty;
    [Id(10)] public List<ChatMessage> ChatHistory { get; set; } = new();
}

[GenerateSerializer]
public class PsiOmniGAgentStateLogEvent : StateLogEventBase<PsiOmniGAgentStateLogEvent>
{
    [Id(0)] public string UniqueId { get; set; } = Guid.NewGuid().ToString();
}

[GenerateSerializer]
public class UpdateSendConfigEvent : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public AgentConfigEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class ReceiveUserMessageEvent : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public UserMessageEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class ReceiveAgentMessageEvent : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public AgentMessageEvent Event { get; set; } = new();
}

[GenerateSerializer]
public class NewAgentsCreatedEvent : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public List<AgentDescriptor> NewAgents { get; set; } = new();
}

[GenerateSerializer]
public class UpdateChildEvent : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public AgentDescriptor LastChildDescriptor { get; set; } = new();
}

[GenerateSerializer]
public class GrowChatHistoryEvent : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public List<ChatMessage> NewMessages { get; set; } = new();
}

[GenerateSerializer]
public class RealizationEvent : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public RealizationStatus RealizationStatus { get; set; } = RealizationStatus.Unrealized;
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public List<ToolDefinition> Tools { get; set; } = new();
}

[GenerateSerializer]
public class UpdateSelfDescription : PsiOmniGAgentStateLogEvent
{
    [Id(0)] public string Description { get; set; } = string.Empty;
}

[GAgent("psi", "omni")]
public class PsiOmniGAgent : GAgentBase<PsiOmniGAgentState, PsiOmniGAgentStateLogEvent>
{
    private static Dictionary<RealizationStatus, string> SytemPrompts = new Dictionary<RealizationStatus, string>()
    {
        [RealizationStatus.Unrealized] = """
                                         You are a professional analyst that analyzes the task given by the user. You help an
                                         agent to decide if it will operate in ORCHESTRATOR or SPECIALIZED mode.

                                         ## ORCHESTRATOR MODE
                                         - The agent will not perform any specific task. It will break down the task into sub-tasks and create child agents to handle the sub-tasks.
                                         - The child agents can be re-used to perform similar sub-tasks.

                                         ## SPECIALIZED MODE
                                         - The agent will perform a specific task. It will use the tools given to it to perform the task.
                                         - The agent will not create child agents.

                                         ## Output Format
                                         - Output a JSON object with the following fields:
                                            - "OperationMode": "ORCHESTRATOR" or "SPECIALIZED"
                                            - "Description": a description of the agent can do. For SPECIALIZED agents: 1) Include the agent's capability derived from the selected tools. 2) DO NOT directly include the task without generalization.
                                            - "Tools": a list of names of the tools the agent will use (only for SPECIALIZED mode)
                                         - No other text or explanation.
                                         """,
        [RealizationStatus.Orchestrator] = """
                                           You are a smart orchestrator agent that can interact with the user, analyze the user's request,
                                           break down the request into sub-tasks and delegate the sub-tasks to child agents.

                                           ## General Rules
                                           - Always analyze the user's request and break it down into sub-tasks.
                                           - Perform the task by delegating sub-tasks to child agents.
                                           - Make sure to consider existing child agents before deciding to delegate a sub-task.
                                           - Decide suitable child agents based on their description and tools used.
                                           - Only use tools for interacting with child agents and no other tools.
                                           - Never send the original task to another agent unless it's a simple task that is suitable for an existing agent.

                                           ## Guidelines for Task Breakdown and Delegation
                                           - Apply separation of concerns. An agent should be responsible for one type of task.
                                           - When you send the first task to a new agent, the agent is created upon receiving the task, you don't need to send the task in another tool call. IMPORTANT: Always try to find an existing agent that is suitable for a task first.
                                           - New agent is required if and only if a new category of subtasks is discovered.

                                           ## Minimize interaction with the user
                                           - Don't be verbose and keep asking for confirmation from the user.
                                           - Apply your best judgement to create agents without asking for permission.

                                           ## Output Format
                                           - Output a JSON object with the following fields:
                                              - "Intermediate": the intermediate result of the agent.
                                              - "Final": the final result of the agent.
                                           - Either "Intermediate" or "Final" must be present, not both.
                                           - If the task is not finished, you should output "Intermediate" with the intermediate result.
                                           - If the task is finished, you should output "Final" with the final result.

                                           ## Example Outputs
                                           {
                                             "Intermediate": "There are 22 people in the room and we have 2 cakes. We need to divide the cakes evenly.",
                                             "Final": "We have 11 people and 1 cake each."
                                           }
                                           {
                                             "Intermediate": "I received the GDP of the United States for 2024 which is $x trillion. Awaiting the GDP of New York state for 2024 before I can calculate the percentage contribution of New York state to the US GDP."
                                           }
                                           {
                                             "Final": "The GDP of the United States for 2024 is $x trillion, and the GDP of New York state for 2024 is $y trillion. The percentage contribution of New York state to the US GDP is approximately z%."
                                           }

                                           """,
        [RealizationStatus.Specialized] = "" // TODO:
    };

    private string INTROSPECTOR_SYSTEM_PROMPT = """
                                                You are an agent manager that understands the capabilities of the agents.

                                                ## Task
                                                - You are trying to understand the capabilities of an agent that works as an orchestrator and delegates its agent.
                                                - Derive the capabilities of the agent from the capabilities of the child agents.
                                                - Prepare a description of the agent's capabilities.
                                                - Understand the category of tasks the agent can handle.
                                                - Avoid putting specific tasks in the description.
                                                """;

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
    private readonly IOrchestratorService _orchestratorService;
    private readonly IToolService _toolService;
    private readonly HashSet<string> _receivedMessageIds = new HashSet<string>();

    public PsiOmniGAgent(
        IKernelFactory kernelFactory,
        IGAgentFactory gAgentFactory,
        IOrchestratorService orchestratorService,
        IToolService toolService
    )
    {
        _kernelFactory = kernelFactory;
        _gAgentFactory = gAgentFactory;
        _orchestratorService = orchestratorService;
        _toolService = toolService;
    }

    public override Task<string> GetDescriptionAsync()
    {
        throw new NotImplementedException();
    }

    [EventHandler]
    public async Task HandleSendConfigEventAsync(AgentConfigEvent @event)
    {
        _orchestratorService.SetConfiguration(@event.Configuration);
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

    [EventHandler]
    public async Task HandleSelfReportEventAsync(SelfReportEvent @event)
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

        RaiseEvent(new UpdateChildEvent()
        {
            LastChildDescriptor = @event.SelfReport
        });
        await ConfirmEvents();
    }

    private async Task DoSelfReportAsync()
    {
        if (State.UserAgentId.IsNullOrEmpty())
        {
            // Do Nothing
            return;
        }

        await PublishAsync(GrainId.Parse(State.UserAgentId), new SelfReportEvent
        {
            TargetAgentId = State.UserAgentId,
            SelfReport = new AgentDescriptor
            {
                AgentId = State.AgentId,
                AgentType = State.RealizationStatus == RealizationStatus.Orchestrator ? "orchestrator" : "specialized",
                Description = State.Description,
                Examples = State.Examples,
                Tools = State.Tools
            }
        });
    }

    private async Task RunAsync(string trigger = null)
    {
        if (!InitializedOk())
        {
            // Do nothing
            return;
        }

        Kernel kernel;
        ChatHistory chatHistory;
        int preHistoryLength;
        var systemPrompt = SytemPrompts[State.RealizationStatus];
        switch (State.RealizationStatus)
        {
            case RealizationStatus.Unrealized:
                kernel = GetKernel_Analyzer();
                systemPrompt += $"\n\n## Available Tools:\n{GetAllToolDefinitions()}";
                (chatHistory, preHistoryLength) = await RunCoreAsync(kernel, systemPrompt);
                OnChatDoneAsync_Analyzer(chatHistory, preHistoryLength);
                break;
            case RealizationStatus.Orchestrator:
                kernel = GetKernel_Orchestrator();
                systemPrompt += $"\n\n## Existing Child Agents (Try your best to re-use them):\n{GetAllChildAgents()}";
                systemPrompt += $"\n\nYour agent Id is: <agentId>{this.GetGrainId()}</agentId>";
                (chatHistory, preHistoryLength) = await RunCoreAsync(kernel, systemPrompt);
                OnChatDoneAsync_Orchestrator(chatHistory, preHistoryLength);
                break;
            case RealizationStatus.Specialized:
                kernel = GetKernel_Specialized();
                (chatHistory, preHistoryLength) = await RunCoreAsync(kernel, systemPrompt);
                OnChatDoneAsync_Specialized(chatHistory, preHistoryLength);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private bool InitializedOk()
    {
        if (State.ChatHistory.IsNullOrEmpty())
        {
            return false;
        }

        if (State.Configuration == null)
        {
            return false;
        }

        return true;
    }

    #region Introspector

    private Kernel GetKernel_Plain()
    {
        var kernel = _kernelFactory.CreateKernel(
            State.Configuration!
        ); // Orchestrator doesn't have specialized tools.
        if (kernel == null)
            throw new InvalidOperationException("Kernel is not configured for tool execution.");

        return kernel;
    }

    private async Task RunIntrospectionAsync()
    {
        var kernel = GetKernel_Plain();
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(INTROSPECTOR_SYSTEM_PROMPT);
        chatHistory.AddUserMessage(
            $"Prepare a description for the agent with the following child agents:\n{GetChildrenDescriptions()}");
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
        var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
        chatHistory.Add(result);
        if (result.Content != null)
            RaiseEvent(new UpdateSelfDescription
            {
                Description = result.Content
            });
    }

    private string GetChildrenDescriptions()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return serializer.Serialize(State.ChildAgents.Values.ToList());
    }

    #endregion Introspector

    #region Analyzer

    private Kernel GetKernel_Analyzer()
    {
        var kernel = _kernelFactory.CreateKernel(
            State.Configuration!
        ); // Orchestrator doesn't have specialized tools.
        if (kernel == null)
            throw new InvalidOperationException("Kernel is not configured for tool execution.");

        kernel.Plugins.AddFromObject(_toolService, "AgentServices");
        return kernel;
    }

    private string GetAllToolDefinitions()
    {
        if (_kernelFactory.FunctionRegistry == null) return string.Empty;
        var toolDefinitions = _kernelFactory.FunctionRegistry!.GetAllToolDefinitions();
        // return JsonSerializer.Serialize(toolDefinitions);
        return ConvertJsonElementListToYaml(toolDefinitions);
    }

    private string GetAllChildAgents()
    {
        var children = State.ChildAgents.Values.ToList();

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return serializer.Serialize(children);
    }

    public static string ConvertJsonElementListToYaml(List<JsonElement> jsonElements)
    {
        // Manually convert each JsonElement to a plain .NET object.
        var listOfDotnetObjects = jsonElements.Select(el => ConvertToPlainObject(el)).ToList();

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return serializer.Serialize(listOfDotnetObjects);
    }

// This is our robust, manual converter.
    private static object? ConvertToPlainObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                // For objects, create a Dictionary
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = ConvertToPlainObject(property.Value);
                }

                return dict;

            case JsonValueKind.Array:
                // For arrays, create a List
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertToPlainObject(item));
                }

                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                return element.GetDecimal(); // Or GetDouble(), GetInt32(), etc. as appropriate

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
                return null;

            case JsonValueKind.Undefined:
            default:
                return null; // Or throw an exception if you want to be strict
        }
    }

    private void OnChatDoneAsync_Analyzer(ChatHistory chatHistory, int preChatHistoryLength)
    {
        var result = chatHistory.Last().Content ?? string.Empty;
        if (result.Contains("ORCHESTRATOR") || result.Contains("SPECIALIZED"))
        {
            var jsonStartIndex = result.IndexOf('{');
            var jsonEndIndex = result.LastIndexOf('}');
            if (jsonStartIndex != -1 && jsonEndIndex != -1)
            {
                result = result.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
            }

            var realizationResult = JsonSerializer.Deserialize<RealizationResult>(result);
            if (realizationResult?.OperationMode == "ORCHESTRATOR")
            {
                RaiseEvent(new RealizationEvent
                {
                    RealizationStatus = RealizationStatus.Orchestrator,
                    Description = realizationResult?.Description ?? string.Empty // Orchestrator doesn't have tools.
                });
            }
            else if (realizationResult?.OperationMode == "SPECIALIZED")
            {
                var tools = new List<ToolDefinition>();
                foreach (var toolName in realizationResult.Tools)
                {
                    var kernelFunction = _kernelFactory.FunctionRegistry?.GetToolByQualifiedName(toolName);
                    if (kernelFunction != null)
                    {
                        tools.Add(kernelFunction.ToToolDefinition());
                    }
                }

                RaiseEvent(new RealizationEvent()
                {
                    RealizationStatus = RealizationStatus.Specialized,
                    Description = realizationResult?.Description ?? string.Empty,
                    Tools = tools
                });
            }
        }
    }

    #endregion Analyzer

    #region Converters

    private static ChatMessage ConvertOpenAiChatMessage(OpenAIChatMessageContent content)
    {
        var json = JsonSerializer.Serialize(content);
        var serialized = new SerializedChatMessageContent()
        {
            TypeFullName = typeof(OpenAIChatMessageContent).FullName,
            Json = json
        };
        return new ChatMessage(content.Role.ToString(), content.Content)
        {
            Serialized = serialized
        };
    }

    private static ChatMessage ConvertToolCallMessage(ChatMessageContent m)
    {
        var message = new ChatMessage(m.Role.ToString(), m.Content);
        var functionResult = m.Items.OfType<FunctionResultContent>().FirstOrDefault();
        if (functionResult == null) return message;
        message.Metadata[OpenAIChatMessageContent.ToolIdProperty] = functionResult.CallId ?? string.Empty;
        message.Metadata["FunctionName"] = functionResult.FunctionName ?? string.Empty;
        return message;
    }

    #endregion

    #region Orchestrator

    private Kernel GetKernel_Orchestrator()
    {
        var kernel = _kernelFactory.CreateKernel(
            State.Configuration!
        ); // Orchestrator doesn't have specialized tools.
        if (kernel == null)
            throw new InvalidOperationException("Kernel is not configured for tool execution.");

        kernel.Plugins.AddFromObject(_orchestratorService, "AgentServices");
        return kernel;
    }

    private void OnChatDoneAsync_Orchestrator(ChatHistory chatHistory, int preChatHistoryLength)
    {
        List<AgentDescriptor> FishAgentCreationEvents(IList<ChatMessage> newMessages)
        {
            bool IsAgentCreation(ChatMessage message)
            {
                return message.Role == "tool" &&
                       message.Metadata.TryGetValue("FunctionName", out var funcNameObject) &&
                       funcNameObject is string funcName && funcName == "create_and_call_agent" &&
                       message.Content.IndexOf('{') >= 0;
            }

            return newMessages.Where(IsAgentCreation).Select(message =>
                JsonSerializer.Deserialize<AgentDescriptor>(
                    message.Content.Substring(message.Content.IndexOf('{'))
                )
            ).Where(x => x != null).Select(x => x!).ToList();
        }

        var newMessages = chatHistory.Skip(preChatHistoryLength)
            .Select(m =>
            {
                if (m is OpenAIChatMessageContent mm)
                {
                    return ConvertOpenAiChatMessage(mm);
                }

                if (m.Role == AuthorRole.Tool)
                {
                    return ConvertToolCallMessage(m);
                }

                return new ChatMessage(m.Role.ToString(), m.Content);
            })
            .ToList();

        var newAgents = FishAgentCreationEvents(newMessages);
        if (newAgents.Count > 0)
        {
            RaiseEvent(new NewAgentsCreatedEvent
            {
                NewAgents = newAgents
            });
        }

        RaiseEvent(new GrowChatHistoryEvent()
        {
            NewMessages = newMessages
        });
    }

    #endregion Orchestrator

    #region Specialized

    private Kernel GetKernel_Specialized()
    {
        var kernel = _kernelFactory.CreateKernel(
            State.Configuration!,
            State.Tools.Select(x => x.Name).ToList()
        ); // Orchestrator doesn't have specialized tools.
        if (kernel == null)
            throw new InvalidOperationException("Kernel is not configured for tool execution.");

        return kernel;
    }

    private void OnChatDoneAsync_Specialized(ChatHistory chatHistory, int preChatHistoryLength)
    {
        var newMessages = chatHistory.Skip(preChatHistoryLength)
            .Select(m =>
            {
                if (m is OpenAIChatMessageContent mm)
                {
                    return ConvertOpenAiChatMessage(mm);
                }

                if (m.Role == AuthorRole.Tool)
                {
                    return ConvertToolCallMessage(m);
                }

                return new ChatMessage(m.Role.ToString(), m.Content);
            })
            .ToList();

        RaiseEvent(new GrowChatHistoryEvent()
        {
            NewMessages = newMessages
        });
    }

    #endregion Specialized

    private async Task<(ChatHistory, int)> RunCoreAsync(Kernel kernel, string systemPrompt)
    {
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

        var chatHistory = GetChatHistory(systemPrompt);
        var preChatHistoryLength = chatHistory.Count;
        var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
        chatHistory.Add(result);
        return (chatHistory, preChatHistoryLength);
    }

    private async Task ReplyAsync(string finalResult)
    {
        if (State.UserAgentId.IsNullOrEmpty())
        {
            Logger.LogInformation("Result:\n{Result}", State.ChatHistory.Last()?.Content);
            return;
        }

        await PublishAsync(GrainId.Parse(State.UserAgentId), new AgentMessageEvent
        {
            TargetAgentId = State.UserAgentId,
            CallId = State.CallId,
            Content = finalResult
        });
    }

    protected override void GAgentTransitionState(
        PsiOmniGAgentState state,
        StateLogEventBase<PsiOmniGAgentStateLogEvent> @event
    )
    {
        if (@event is PsiOmniGAgentStateLogEvent e1)
        {
            var uid = e1.UniqueId;
            if (!_receivedMessageIds.Add(uid))
            {
                return;
            }
        }

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
                    // state.Tools = payload.Event.Tools; // Not needed here. No tools should be configured here.
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
                    state.UserAgentId = payload.Event.ReplyToAgentId;
                    var message = ChatMessage.CreateUserMessage(payload.Event.Content);
                    message.Metadata["CallId"] = payload.Event.CallId;
                    state.ChatHistory.Add(message);
                    state.Examples.Add(new AgentExample
                    {
                        Request = payload.Event.Content,
                        Response = String.Empty
                    });
                    DoAsync(async () => await RunAsync($"User Message {payload.Event}"));
                }

                break;
            case RealizationEvent payload:
                if (state.RealizationStatus == RealizationStatus.Unrealized)
                {
                    state.RealizationStatus = payload.RealizationStatus;
                    state.Description = payload.Description;
                    state.Tools = payload.Tools;
                }

                DoAsync(async () =>
                {
                    await DoSelfReportAsync();
                    await RunAsync("RealizationEvent");
                });
                break;
            case ReceiveAgentMessageEvent payload:
                var amessage =
                    ChatMessage.CreateAssistantMessage(
                        $"Received reply for callId ({payload.Event.CallId}): {payload.Event.Content}");
                amessage.Metadata["CallId"] = payload.Event.CallId;
                state.ChatHistory.Add(amessage);
                DoAsync(async () => await RunAsync($"Agent Message {payload.Event}"));
                break;
            case NewAgentsCreatedEvent payload:
                foreach (var newAgent in payload.NewAgents)
                {
                    state.ChildAgents.TryAdd(newAgent.AgentId, newAgent);
                }

                break;
            case UpdateChildEvent payload:
                AgentDescriptor? oldObj;
                // Child may proceed first and we receive this event before we process our own NewAgentsCreatedEvent event
                if (!state.ChildAgents.TryGetValue(payload.LastChildDescriptor.AgentId, out oldObj))
                {
                    oldObj = new AgentDescriptor()
                    {
                        AgentId = payload.LastChildDescriptor.AgentId
                    };
                    state.ChildAgents[payload.LastChildDescriptor.AgentId] = oldObj;
                }

                var oldObjClone = oldObj.DeepClone();
                var newObjClone = payload.LastChildDescriptor.DeepClone();
                oldObjClone.Examples = new List<AgentExample>();
                newObjClone.Examples = new List<AgentExample>();
                var refreshDescription = !oldObjClone.Equals(newObjClone);

                state.ChildAgents[payload.LastChildDescriptor.AgentId] = payload.LastChildDescriptor;
                DoAsync(async () =>
                {
                    if (refreshDescription)
                    {
                        await RunIntrospectionAsync();
                    }

                    _orchestratorService.UpdateChildAgents(this.GetGrainId().ToString(),
                        State.ChildAgents.Values.ToList());
                });
                break;
            case GrowChatHistoryEvent payload:
                state.ChatHistory.AddRange(payload.NewMessages);
                if (state.ChatHistory.Count <= 1)
                    break;
                var finalResult = string.Empty;

                if (State.RealizationStatus == RealizationStatus.Specialized)
                {
                    finalResult = State.ChatHistory.Last().Content;
                }
                else if (State.RealizationStatus == RealizationStatus.Orchestrator)
                {
                    var lastMessage = State.ChatHistory.Last()?.Content ?? string.Empty;
                    try
                    {
                        var lastOrchestratorMessage = JsonSerializer.Deserialize<OrchestratorMessage>(lastMessage);
                        if (lastOrchestratorMessage != null && !lastOrchestratorMessage.Final.IsNullOrEmpty())
                        {
                            finalResult = lastOrchestratorMessage.Final;
                        }
                    }
                    catch (Exception ex)
                    {
                        finalResult = lastMessage;
                        Logger.LogError(ex, "Failed to deserialize OrchestratorMessage: {Message}", lastMessage);
                    }
                }

                if (!finalResult.IsNullOrEmpty())
                {
                    state.Examples.Last().Response = finalResult;
                    DoAsync(async () =>
                    {
                        // TODO: Maybe update description.
                        await DoSelfReportAsync();
                        await ReplyAsync(finalResult);
                    });
                }

                break;
            case UpdateSelfDescription payload:
                state.Description = payload.Description;
                DoAsync(DoSelfReportAsync);
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

    private ChatHistory GetChatHistory(string systemPrompt)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
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