using Aevatar.Core.Abstractions;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Common;


[GenerateSerializer]
public class AgentConfigEvent : EventBase
{
    [Id(0)] public AgentConfiguration Configuration { get; set; } = new();
    [Id(1)] public string ParentAgentId { get; set; } = string.Empty;
    [Id(2)] public List<string> Tools { get; set; } = new();
}

/// <summary>
/// User agent sends to target agent
/// </summary>
[GenerateSerializer]
public class UserMessageEvent : EventBase
{
    [Id(0)] public string TargetAgentId { get; set; }
    [Id(1)] public string CallId { get; set; }
    [Id(2)] public string Content { get; set; }
}

/// <summary>
/// Agent's reply message
/// </summary>
[GenerateSerializer]
public class AgentMessageEvent : EventBase
{
    [Id(0)] public string TargetAgentId { get; set; }
    [Id(1)] public string CallId { get; set; }
    [Id(2)] public string Content { get; set; }
}