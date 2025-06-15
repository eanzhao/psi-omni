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
public class UpdateTaskAnalysicResultEvent : AgentStateLogEvent
{
    [Id(0)] public TaskAnalysisResult TaskAnalysisResult { get; set; } = new();
}