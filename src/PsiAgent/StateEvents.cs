using Aevatar.Core.Abstractions;
using PsiOrleans.Common.Models;

namespace PsiAgent;

[GenerateSerializer]
public class AgentStateLogEvent : StateLogEventBase<AgentStateLogEvent>;

[GenerateSerializer]
public class UpdateSendConfigEvent : AgentStateLogEvent
{
    [Id(0)] public SendConfigEvent SendConfigEvent { get; set; } = new();
}

[GenerateSerializer]
public class UpdateTaskEvent : AgentStateLogEvent
{
    [Id(0)] public string CallId { get; set; } = string.Empty;
    [Id(1)] public string Task { get; set; } = string.Empty;
}

[GenerateSerializer]
public class UpdateTaskAnalysicResultEvent : AgentStateLogEvent
{
    [Id(0)] public TaskAnalysisResult TaskAnalysisResult { get; set; } = new();
}

[GenerateSerializer]
public class UpdateSpecializedRunResultEvent : AgentStateLogEvent
{
    [Id(0)] public List<ChatMessage> ChatHistory { get; set; } = new();
}

[GenerateSerializer]
public class UpdateSubTasksEvent : AgentStateLogEvent
{
    [Id(0)] public List<SubTask> SubTasks { get; set; } = new();
}

[GenerateSerializer]
public class UpdateSubTaskCallbackDatasEvent : AgentStateLogEvent
{
    [Id(0)] public List<CallbackData> CallbackDatas { get; set; } = new();
}

[GenerateSerializer]
public class ReceiveCallbackEvent : AgentStateLogEvent
{
    [Id(0)] public TaskCallbackEvent TaskCallbackEvent { get; set; } = new();
}

[GenerateSerializer]
public class UpdateOrchestratorChatEvent : AgentStateLogEvent
{
    [Id(0)] public List<ChatMessage> Messages { get; set; } = new();
    [Id(1)] public bool IsFinal { get; set; }
    [Id(2)] public string CallId { get; set; } = string.Empty;
}

[GenerateSerializer]
public class UpdateSpecializedChatEvent : AgentStateLogEvent
{
    [Id(0)] public List<ChatMessage> Messages { get; set; } = new();
    [Id(1)] public string CallId { get; set; } = string.Empty;
}

[GenerateSerializer]
public class UpdateConversationStatusEvent : AgentStateLogEvent
{
    [Id(0)] public bool IsInConversation { get; set; }
}

[GenerateSerializer]
public class TaskCompletedStateLogEvent : AgentStateLogEvent
{
}