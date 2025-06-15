using System.Threading.Tasks;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces
{
    /// <summary>
    /// Orleans adapter interface for delegating specialized agent execution to core services.
    /// All business logic must be delegated; no implementation in adapter.
    /// </summary>
    public interface ISpecializedOrleansAdapter
    {
        /// <summary>
        /// Initiates async specialized tool execution. Returns immediately with callId.
        /// </summary>
        /// <param name="task">Task description</param>
        /// <param name="factory">The Kernel factory.</param>
        /// <param name="state">Agent state</param>
        /// <param name="config">Agent configuration</param>
        /// <param name="callId">Unique call identifier for callback chain</param>
        /// <returns>Task representing the async operation, result is callId or execution token</returns>
        Task<string> StartOrleansSpecializedExecutionAsync(
            string task,
            IKernelFactory factory,
            UnifiedAgentState state,
            AgentConfiguration config,
            string callId,
            IEnumerable<string> toolNames);
    }
} 