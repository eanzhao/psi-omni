using Aevatar.Core.Abstractions;
using Orleans;
using PsiOrleans.Common.Models;
using PsiOrleans.Common.Interfaces;

namespace PsiOrleans.Common.Models;

[Serializable]
[GenerateSerializer]
public class OrchestratorState
{
    [Id(0)] public List<SubTask> CurrentSubTasks { get; set; } = new();
    [Id(1)] public Dictionary<string, CallbackData> PendingCallbacks { get; set; } = new();
    [Id(2)] public List<CompletedCallback> CompletedCallbacks { get; set; } = new();
    [Id(3)] public string? ExecutionPlan { get; set; }
    [Id(4)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Id(5)] public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

[Serializable]
[GenerateSerializer]
public class SpecializedState
{
    /// <summary>
    /// Chat history for this agent
    /// </summary>
    [Id(0)]
    public List<ChatMessage> ChatHistory { get; set; } = new();
}

[Serializable]
[GenerateSerializer]
public class AgentState : StateBase
{
    [Id(0)] public string AgentId { get; set; } = string.Empty;
    [Id(1)] public string ParentAgentId { get; set; } = string.Empty;
    [Id(2)] public string Task { get; set; } = string.Empty;
    [Id(3)] public string? SpecializedAgentId { get; set; } = string.Empty;
    [Id(4)] public string CallId { get; set; } = string.Empty;
    [Id(5)] public AgentConfiguration? Configuration { get; set; }
    [Id(6)] public AgentRole AgentRole { get; set; } = AgentRole.Undecided;
    [Id(7)] public OrchestratorState Orchestrator { get; set; } = new();
    [Id(8)] public TaskAnalysisResult TaskAnalysisResult { get; set; } = new();
    [Id(9)] public SpecializedState? SpecializedState { get; set; } = new();
}