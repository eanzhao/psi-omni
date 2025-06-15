using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using PsiOrleans.Common.Models;
using PsiOrleans.Common.Interfaces;
using System.Collections.Generic;

namespace PsiOrleans.Specialized.Services
{
    public interface ISpecializedAgentStrategy
    {
        /// <summary>
        /// Starts async execution of a task, returns immediately, and handles callback invocation on completion.
        /// </summary>
        /// <param name="task">The task description.</param>
        /// <param name="factory">The Kernel factory.</param>
        /// <param name="state">The agent state.</param>
        /// <param name="config">The agent configuration.</param>
        /// <param name="callId">The call ID for callback.</param>
        /// <param name="toolNames">The optional tool names to inject.</param>
        /// <returns>Immediate response string (e.g., "Tool execution initiated").</returns>
        Task<string> StartAsyncExecution(string task, IKernelFactory context, AgentState state, AgentConfiguration config, string callId, IEnumerable<string>? toolNames = null);
    }
}