namespace PsiGAgent.Omni;

[GenerateSerializer]
public class RealizationResult
{
    [Id(0)] public string OperationMode { get; set; } = "UNKNOWN";
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public List<string> Tools { get; set; } = new();
}

[GenerateSerializer]
public class ToolDefinition
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public List<string> Parameters { get; set; } = new();
}