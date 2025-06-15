using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Orchestrator.Services;

public class InMemoryAgentCommunicationService : IAgentCommunicationService
{
    public readonly ConcurrentDictionary<string, AgentState> _agents = new();
    private readonly ConcurrentDictionary<string, List<(string callId, string result, bool isSuccess)>> _callbacks = new();
    private readonly IAgentInvocationService _agentInvocationService;
    private OrchestratorService? _orchestrator;
    private readonly ConcurrentDictionary<string, object> _agentLocks = new();

    public InMemoryAgentCommunicationService(IAgentInvocationService agentInvocationService)
    {
        _agentInvocationService = agentInvocationService;
    }

    public Task<string> CreateAgentAsync(AgentConfiguration config, string? parentAgentId = null)
    {
        var agentId = $"agent-{Guid.NewGuid()}";
        var state = new AgentState
        {
            AgentId = agentId,
            ParentAgentId = parentAgentId ?? string.Empty,
            Configuration = config,
            AgentRole = AgentRole.Undecided,
            Orchestrator = new OrchestratorState()
        };
        _agents[agentId] = state;
        return Task.FromResult(agentId);
    }

    public async Task<string> CallAgentAsync(string agentId, string task, string callId, string parentAgentId)
    {
        if (_agents.TryGetValue(agentId, out var state))
        {
            state.Task = task;
            // 递归真实调用 agent invocation service，直接传递 state 引用
            state.ParentAgentId = parentAgentId;
            var result = await _agentInvocationService.InvokeAgentAsync(state);
            // await SendCallbackAsync(parentAgentId, callId, result.Result, result.Success);
        }
        return callId;
    }

    public void RegisterOrchestrator(OrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public async Task SendCallbackAsync(string parentAgentId, string callId, string result, bool isSuccess)
    {
        _callbacks.AddOrUpdate(parentAgentId,
            _ => new List<(string, string, bool)> { (callId, result, isSuccess) },
            (_, list) => { list.Add((callId, result, isSuccess)); return list; });
        // 主动驱动 orchestrator 回调处理
        if (_orchestrator != null && _agents.TryGetValue(parentAgentId, out var agentState))
        {
            var kernel = agentState.Configuration != null ? _orchestrator._kernelFactory.CreateKernel(agentState.Configuration) : null;
            if (kernel != null)
            {
                var agentLock = _agentLocks.GetOrAdd(parentAgentId, _ => new object());
                lock (agentLock)
                {
                    _orchestrator.ProcessCallbackAsync(callId, result, isSuccess, agentState, kernel).GetAwaiter().GetResult();
                }
            }
        }
    }

    public Task<AgentState?> GetAgentStateAsync(string agentId)
    {
        _agents.TryGetValue(agentId, out var state);
        return Task.FromResult(state);
    }

    // For e2e: 获取所有回调
    public List<(string callId, string result, bool isSuccess)> GetCallbacks(string agentId)
        => _callbacks.TryGetValue(agentId, out var list) ? list : new();
} 