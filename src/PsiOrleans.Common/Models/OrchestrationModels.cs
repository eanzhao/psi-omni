using System;
using System.Collections.Generic;
using Orleans;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Orchestration decision types for LLM-based orchestration logic
/// </summary>
[GenerateSerializer]
public enum OrchestrationDecision
{
    CompleteTask,
    WaitForMoreCallbacks,
    CreateAdditionalTasks,
    RetryTimeouts
}

[Serializable]
[GenerateSerializer]
public class SubTask
{
    [Id(0)] public string Task { get; set; } = string.Empty;
    [Id(1)] public AgentRole SuggestedRole { get; set; } = AgentRole.Specialized;
    [Id(2)] public List<string> RequiredTools { get; set; } = new();
    [Id(3)] public int Priority { get; set; } = 1;
    [Id(4)] public string? ChildAgentId { get; set; }
    [Id(5)] public string SubTaskId { get; set; } = Guid.NewGuid().ToString();
    [Id(6)] public SubTaskStatus Status { get; set; } = SubTaskStatus.Pending;
    [Id(7)] public List<string> Dependencies { get; set; } = new();
    [Id(8)] public Dictionary<string, string> DependencyResults { get; set; } = new();
    [Id(9)] public bool CanStart { get; set; } = true;
    [Id(10)] public string ReframedTask { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return $"SubTask: {Task}, Status: {Status}, ChildAgentId: {ChildAgentId}, SubTaskId: {SubTaskId}";
    }
}

[GenerateSerializer]
public enum SubTaskStatus
{
    Pending,
    Delegated,
    Completed,
    Failed
}

[Serializable]
[GenerateSerializer]
public class CallbackData
{
    [Id(0)] public string CallId { get; set; } = string.Empty;
    [Id(1)] public string ChildAgentId { get; set; } = string.Empty;
    [Id(2)] public string Task { get; set; } = string.Empty;
    [Id(3)] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Id(4)] public bool IsReceived { get; set; } = false;
    [Id(5)] public string? ResultMessage { get; set; }
    [Id(6)] public bool IsSuccess { get; set; } = false;
    [Id(7)] public DateTime? ReceivedAt { get; set; }
}

[Serializable]
[GenerateSerializer]
public class CompletedCallback
{
    [Id(0)] public string CallId { get; set; } = string.Empty;
    [Id(1)] public string ChildAgentId { get; set; } = string.Empty;
    [Id(2)] public string Task { get; set; } = string.Empty;
    [Id(3)] public string ResultMessage { get; set; } = string.Empty;
    [Id(4)] public bool IsSuccess { get; set; } = false;
    [Id(5)] public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    [Id(6)] public TimeSpan ExecutionTime { get; set; }
} 