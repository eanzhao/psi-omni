using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces;

/// <summary>
/// Defines the contract for orchestrating and coordinating multiple agents.
/// Implemented by the Orchestrator package to provide agent coordination capabilities.
/// </summary>
public interface IOrchestrator
{
    /// <summary>
    /// Orchestrates a complex task by dynamically creating and coordinating specialized agents.
    /// This is the primary orchestration method that handles task decomposition, agent creation, and coordination.
    /// </summary>
    /// <param name="taskDescription">The description of the complex task requiring orchestration.</param>
    /// <param name="context">The agent context providing orchestration context.</param>
    /// <returns>The result of the orchestration including execution plan, created agents, and final result.</returns>
    Task<OrchestratorResult> OrchestrateTaskAsync(string taskDescription, IAgentContext context);

    /// <summary>
    /// Assigns a specific task to a target agent.
    /// </summary>
    /// <param name="targetAgent">The identifier of the agent to receive the task assignment.</param>
    /// <param name="taskDescription">The description of the task to assign.</param>
    /// <param name="context">The agent context providing assignment context.</param>
    /// <returns>True if the task was successfully assigned, false otherwise.</returns>
    Task<bool> AssignTaskAsync(AgentId targetAgent, string taskDescription, IAgentContext context);
} 