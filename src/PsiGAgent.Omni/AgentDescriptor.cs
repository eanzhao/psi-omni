namespace PsiGAgent.Omni;

[Serializable]
[GenerateSerializer]
public class AgentExample
{
    [Id(0)] public string Request { get; set; } = string.Empty;
    [Id(1)] public string Response { get; set; } = string.Empty;
}

[Serializable]
[GenerateSerializer]
public class AgentDescriptor
{
    [Id(0)] public string AgentId { get; set; } = string.Empty;
    [Id(1)] public string AgentType { get; set; } = string.Empty; // Orchestrator, Specialized
    [Id(2)] public string Description { get; set; } = string.Empty;
    [Id(3)] public List<AgentExample> Examples { get; set; } = new();
    [Id(4)] public List<string> Tools { get; set; } = new();
}