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