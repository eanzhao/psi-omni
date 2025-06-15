using System.Threading.Tasks;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Orchestrator.Services;

public interface IAgentCommunicationService
{
    Task<string> CreateAgentAsync(AgentConfiguration config, string? parentAgentId = null);
    Task<string> CallAgentAsync(string agentId, string task, string callId, string parentAgentId);
    Task SendCallbackAsync(string parentAgentId, string callId, string result, bool isSuccess);
    Task<AgentState?> GetAgentStateAsync(string agentId);
} 