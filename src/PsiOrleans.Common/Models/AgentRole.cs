namespace PsiOrleans.Common.Models;

/// <summary>
/// Agent roles based on task analysis and orchestration requirements
/// </summary>
public enum AgentRole
{
    /// <summary>
    /// Agent role has not been determined yet (initial state)
    /// </summary>
    Undecided = 0,
    
    /// <summary>
    /// Agent orchestrates complex tasks by delegating to child agents
    /// Can use: SendParentCallback, CreateAgent, CallChildAgent
    /// Cannot use: Normal blocking tools
    /// </summary>
    Orchestrator = 1,
    
    /// <summary>
    /// Agent specializes in handling specific tasks directly
    /// Can use: Normal blocking tools, SendParentCallback
    /// Cannot use: CreateAgent, CallChildAgent
    /// </summary>
    Specialized = 2
}

/// <summary>
/// Extension methods for AgentRole enum
/// </summary>
public static class AgentRoleExtensions
{
    /// <summary>
    /// Determines if the agent role has been decided (not Undecided)
    /// </summary>
    public static bool IsDecided(this AgentRole role) => role != AgentRole.Undecided;

    /// <summary>
    /// Determines if the agent can create and manage child agents
    /// </summary>
    public static bool CanCreateChildAgents(this AgentRole role) => role == AgentRole.Orchestrator;

    /// <summary>
    /// Determines if the agent can use direct tools for task execution
    /// </summary>
    public static bool CanUseDirectTools(this AgentRole role) => role == AgentRole.Specialized;
} 