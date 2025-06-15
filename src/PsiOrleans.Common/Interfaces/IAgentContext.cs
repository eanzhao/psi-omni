using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces;

/// <summary>
/// Provides context and identity information for agent operations.
/// Serves as the foundation for all agent interactions within the system.
/// </summary>
public interface IAgentContext
{
    /// <summary>
    /// Gets the unique identifier for this agent.
    /// </summary>
    AgentId AgentId { get; }

    /// <summary>
    /// Gets the human-readable name of this agent.
    /// </summary>
    string AgentName { get; }

    /// <summary>
    /// Gets the role/type of this agent in the system.
    /// </summary>
    AgentRole Role { get; }
} 