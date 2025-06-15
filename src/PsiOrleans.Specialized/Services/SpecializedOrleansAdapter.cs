using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Specialized.Services
{
    // /// <summary>
    // /// Orleans 适配层接口的实现，桥接 SpecializedAgentStrategy。
    // /// </summary>
    // public class SpecializedOrleansAdapter : ISpecializedOrleansAdapter
    // {
    //     private readonly ISpecializedAgentStrategy _strategy;
    //     private readonly ICallbackManager _callbackManager;
    //
    //     public SpecializedOrleansAdapter(ISpecializedAgentStrategy strategy, ICallbackManager callbackManager)
    //     {
    //         _strategy = strategy;
    //         _callbackManager = callbackManager;
    //     }
    //
    //     public async Task<string> StartOrleansSpecializedExecutionAsync(string task, IKernelFactory factory, AgentState state,
    //         Common.Models.AgentConfiguration config, string callId, IEnumerable<string> toolNames)
    //     {
    //         var agentState = state; // UnifiedAgentState now implements IAgentState
    //         return await _strategy.StartAsyncExecution(
    //             task,
    //             factory,
    //             state,
    //             // agentState,
    //             config,
    //             callId,
    //             toolNames);
    //     }
    // }
} 