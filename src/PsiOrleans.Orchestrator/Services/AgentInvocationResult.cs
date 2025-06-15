namespace PsiOrleans.Orchestrator.Services;

public class AgentInvocationResult
{
    public string AgentId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Notes { get; set; } = string.Empty;
} 