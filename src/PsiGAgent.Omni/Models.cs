using System.Text.Json.Serialization;

namespace PsiGAgent.Omni;

[GenerateSerializer]
public class RealizationResult
{
    [Id(0)] public string OperationMode { get; set; } = "UNKNOWN";
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public List<string> Tools { get; set; } = new();
}

[GenerateSerializer]
public class AgentCall
{
    [Id(0)] public string AgentId { get; set; } = string.Empty;
    [Id(1)] public string CallId { get; set; } = string.Empty;
    [Id(2)] public string Message { get; set; } = string.Empty;
}

[GenerateSerializer]
public class ToolDefinition
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public List<ToolParameter> Parameters { get; set; } = new();
}

[GenerateSerializer]
public class ToolParameter
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public bool IsRequired { get; set; } = true;
    [Id(3)] public string Schema { get; set; } = string.Empty;
}

[GenerateSerializer]
public class OrchestratorMessage
{
    [Id(0), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Intermediate { get; set; }

    [Id(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Final { get; set; }
}