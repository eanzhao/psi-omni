using Aevatar.Core.Abstractions;
using PsiGAgent.Common;
using PsiGAgent.Common.Models;

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