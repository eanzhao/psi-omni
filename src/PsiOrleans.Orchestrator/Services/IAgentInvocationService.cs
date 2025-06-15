using System.Threading.Tasks;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Orchestrator.Services;

public interface IAgentInvocationService
{
    Task<AgentInvocationResult> InvokeAgentAsync(AgentState agentState);
} 