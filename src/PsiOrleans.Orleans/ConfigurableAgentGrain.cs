using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using PsiOrleans.Common.Models;
using PsiOrleans.Specialized.Services;
using AgentConfiguration = PsiOrleans.Common.Models.AgentConfiguration;
using UnifiedAgentState = PsiOrleans.Common.Models.UnifiedAgentState;
using AgentRole = PsiOrleans.Common.Models.AgentRole;
using PsiOrleans.Common.Interfaces;

namespace PsiOrleans.Orleans
{
    /// <summary>
    /// Lean Orleans grain: minimal glue, delegates all logic to adapters.
    /// </summary>
    public class ConfigurableAgentGrain : IConfigurableAgentGrain
    {
        private readonly ISpecializedOrleansAdapter _specializedAdapter;
        private readonly IOrchestratorOrleansAdapter _orchestratorAdapter;
        private readonly IKernelFactory _kernelFactory;
        private bool _initialized;
        private AgentConfiguration _config = new();
        private string[] _toolNames = Array.Empty<string>();
        private UnifiedAgentState _state = new UnifiedAgentState(Guid.NewGuid().ToString(), new AgentConfiguration());
        private readonly ConcurrentDictionary<string, (string Message, bool IsSuccess)> _pendingCalls = new();
        private AgentRole _role = AgentRole.Specialized;

        public ConfigurableAgentGrain(
            ISpecializedOrleansAdapter specializedAdapter,
            IOrchestratorOrleansAdapter orchestratorAdapter,
            IKernelFactory kernelFactory
        )
        {
            _specializedAdapter = specializedAdapter;
            _orchestratorAdapter = orchestratorAdapter;
            _kernelFactory = kernelFactory;
        }

        /// <inheritdoc />
        public async Task<string> ProcessTaskAsync(string task, string? callId)
        {
            if (!_initialized)
                throw new InvalidOperationException("Agent not initialized");
            if( callId != null)
                callId = Guid.NewGuid().ToString("N");
            if (_role == AgentRole.Orchestrator)
            {
                await _orchestratorAdapter.StartOrleansOrchestratorExecutionAsync(
                    task,
                    null);
            }
            else
            {
                await _specializedAdapter.StartOrleansSpecializedExecutionAsync(
                    task,
                    _kernelFactory,
                    _state,
                    _config,
                    callId,
                    _toolNames);
            }

            return callId;
        }

        /// <inheritdoc />
        public Task<bool> InitializeAsync(AgentConfiguration config, string[] toolNames)
        {
            _config = config;
            _toolNames = toolNames;
            _state = new UnifiedAgentState(Guid.NewGuid().ToString(), _config);
            _initialized = true;
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public Task ReceiveCallbackAsync(string callId, string message, bool isSuccess)
        {
            _pendingCalls[callId] = (message, isSuccess);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<object> GetStateAsync()
        {
            return Task.FromResult((object)_state);
        }

        /// <inheritdoc />
        public Task<bool> IsInitializedAsync()
        {
            return Task.FromResult(_initialized);
        }

        /// <summary>
        /// Sets the agent role (Specialized or Orchestrator) for test/demo purposes.
        /// </summary>
        public void SetRole(AgentRole role)
        {
            _role = role;
        }
    }
}