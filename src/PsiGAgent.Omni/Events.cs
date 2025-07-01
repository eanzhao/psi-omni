using Aevatar.Core.Abstractions;

namespace PsiGAgent.Omni;

[GenerateSerializer]
public class SelfReportEvent : EventBase
{
    [Id(0)] public string UniqueId { get; } = Guid.NewGuid().ToString();
    [Id(1)] public string TargetAgentId { get; set; } = string.Empty;
    [Id(2)] public AgentDescriptor SelfReport { get; set; } = new();
}