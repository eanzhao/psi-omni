using System.Threading.Tasks;
using PsiOrleans.Common.Models;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Specialized.Services;
using System.Linq;

namespace PsiOrleans.Orchestrator.Services;

public class InMemoryAgentInvocationService : IAgentInvocationService
{
    private readonly ITaskAnalyzer _taskAnalyzer;
    private readonly OrchestratorService _orchestratorService;
    private readonly ISpecializedAgentStrategy _specializedAgentStrategy;
    private readonly IAgentCommunicationService _agentCommService;
    private readonly IKernelFactory _kernelFactory;

    public InMemoryAgentInvocationService(
        ITaskAnalyzer taskAnalyzer,
        OrchestratorService orchestratorService,
        ISpecializedAgentStrategy specializedAgentStrategy,
        IAgentCommunicationService agentCommService,
        IKernelFactory kernelFactory)
    {
        _taskAnalyzer = taskAnalyzer;
        _orchestratorService = orchestratorService;
        _specializedAgentStrategy = specializedAgentStrategy;
        _agentCommService = agentCommService;
        _kernelFactory = kernelFactory;
    }

    public async Task<AgentInvocationResult> InvokeAgentAsync(AgentState agentState)
    {
        // 1. 分析
        var analysis = await _taskAnalyzer.AnalyzeTaskAsync(agentState);
        if (analysis.RecommendedApproach == TaskApproach.Orchestration)
        {
            // Orchestrator 路径
            agentState.AgentRole = AgentRole.Orchestrator;
            await _orchestratorService.OrchestrateTaskAsync(agentState);
            return new AgentInvocationResult
            {
                AgentId = agentState.AgentId,
                Role = "Orchestrator",
                Result = agentState.Orchestrator?.ExecutionPlan ?? string.Empty,
                Success = true,
                Notes = analysis.AnalysisNotes
            };
        }
        else
        {
            // Specialized 路径
            agentState.AgentRole = AgentRole.Specialized;
            var callId = System.Guid.NewGuid().ToString();
            // 由 LLM 推荐工具名
            IEnumerable<string>? toolNames = analysis.RecommendedTools;
            var result = await _specializedAgentStrategy.StartAsyncExecution(
                agentState.Task,
                _kernelFactory,
                agentState,
                agentState.Configuration!,
                callId,
                toolNames);
            // Specialized agent 执行后直接通过 agentCommService 发送回调
            _ = Task.Run(() => _agentCommService.SendCallbackAsync(agentState.ParentAgentId, callId, result, true));
            return new AgentInvocationResult
            {
                AgentId = agentState.AgentId,
                Role = "Specialized",
                Result = result,
                Success = true,
                Notes = analysis.AnalysisNotes
            };
        }
    }
} 