namespace PsiGAgent.Common.Models;

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