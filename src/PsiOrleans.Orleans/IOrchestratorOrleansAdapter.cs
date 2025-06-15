using System.Threading.Tasks;
using PsiOrleans.Common.Models; // Assuming state/config types are here

namespace PsiOrleans.Orleans
{
    /// <summary>
    /// Orleans adapter interface for delegating orchestrator agent execution to core services.
    /// All business logic must be delegated; no implementation in adapter.
    /// </summary>
    public interface IOrchestratorOrleansAdapter
    {
        /// <summary>
        /// Initiates async orchestrator execution (task decomposition, delegation, aggregation). Returns execution id.
        /// </summary>
        /// <param name="task">Task description</param>
        /// <param name="context">Orchestration context (should be IAgentContext)</param>
        /// <returns>Task representing the async operation, result is execution id</returns>
        Task<string> StartOrleansOrchestratorExecutionAsync(
            string task,
            object? context);
    }
} 