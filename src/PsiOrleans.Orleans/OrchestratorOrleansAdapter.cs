using System;
using System.Threading.Tasks;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Orleans;
using PsiOrleans.Common.Models;
using PsiOrleans.Specialized.Services;

namespace PsiOrleans.Orchestrator
{
    /// <summary>
    /// Lean adapter: bridges Orleans grain to core orchestrator service.
    /// </summary>
    public class OrchestratorOrleansAdapter : IOrchestratorOrleansAdapter
    {
        private readonly IOrchestrator _orchestrator;

        public OrchestratorOrleansAdapter(IOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public async Task<string> StartOrleansOrchestratorExecutionAsync(
            string task,
            object? context)
        {
            await _orchestrator.OrchestrateTaskAsync(task, context as IAgentContext);
            return Guid.NewGuid().ToString();
        }
    }
} 