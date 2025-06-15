using System;
using System.Threading.Tasks;
using PsiOrleans.Common.Models;
using PsiOrleans.Common.Interfaces;
using System.Collections.Generic;

namespace PsiOrleans.Specialized.Services
{
    public class SpecializedAgentStrategy : ISpecializedAgentStrategy
    {
        private readonly IToolExecutionService _toolExecutionService;

        public SpecializedAgentStrategy(IToolExecutionService toolExecutionService)
        {
            _toolExecutionService = toolExecutionService;
        }

        public Task<string> StartAsyncExecution(string task, IKernelFactory context, AgentState state, AgentConfiguration config, string callId, IEnumerable<string>? toolNames = null)
        {
            return _toolExecutionService.ExecuteAsync(task, context, state, config, toolNames);
        }
    }
} 