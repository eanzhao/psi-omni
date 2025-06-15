#nullable enable
using System.Threading.Tasks;
using PsiOrleans.Common.Models;
using PsiOrleans.Common.Interfaces;
using System.Collections.Generic;

namespace PsiOrleans.Specialized.Services
{

    public interface IToolExecutionService
    {
        /// <summary>
        /// Executes a task using available tools in the provided context and agent state.
        /// </summary>
        /// <param name="task">The task description.</param>
        /// <param name="context">The tool context (framework-agnostic).</param>
        /// <param name="state">The agent state (framework-agnostic).</param>
        /// <param name="config">The agent configuration.</param>
        /// <param name="toolNames">The optional tool names to be used.</param>
        /// <returns>The result of tool execution.</returns>
        Task<string> ExecuteAsync(string task, IKernelFactory factory, AgentState state, AgentConfiguration config, IEnumerable<string>? toolNames = null);
    }
} 