using Aevatar.Core.Abstractions;
using PsiOrleans.Common.Models;

namespace PsiAgent;


[GenerateSerializer]
public class PingEvent : EventBase
{
}

[GenerateSerializer]
public class SendConfigEvent : EventBase
{
    [Id(0)] public AgentConfiguration Configuration { get; set; }
    [Id(1)] public string ParenteAgentId { get; set; }
}


// TODO: Maybe merge with SendConfigEvent
[GenerateSerializer]
public class SendTaskEvent : EventBase
{
    [Id(0)] public string CallId { get; set; }
    [Id(1)] public string Task { get; set; }
}

[GenerateSerializer]
public class TaskCallbackEvent : EventBase
{
    [Id(0)] public string TargetAgentId { get; set; }
    [Id(1)] public string CallId { get; set; }
    [Id(2)] public string Task { get; set; }
    [Id(3)] public ChatMessage Reply { get; set; }
}