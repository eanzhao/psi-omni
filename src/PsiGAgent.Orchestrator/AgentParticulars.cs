namespace PsiGAgent.Orchestrator;

[Serializable]
[GenerateSerializer]
public class AgentParticulars
{
    [Id(0)] public string AgentId { get; set; } = string.Empty;
    [Id(1)] public List<string> Tools { get; set; } = new();
}