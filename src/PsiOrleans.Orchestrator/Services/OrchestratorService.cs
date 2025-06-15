using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;
using PsiOrleans.Specialized.Services;

namespace PsiOrleans.Orchestrator.Services
{
    /// <summary>
    /// Aggregates orchestration logic: task decomposition, progress analysis, result aggregation, dependency management.
    /// Implements IOrchestrator as the single entrypoint for orchestration.
    /// </summary>
    public class OrchestratorService : IOrchestrator
    {
        private readonly ITaskDecomposer _taskDecomposer;
        private readonly IProgressAnalyzer _progressAnalyzer;
        private readonly IResultAggregator _resultAggregator;
        private readonly IDependencyManager _dependencyManager;
        public readonly IKernelFactory _kernelFactory;
        private readonly ISpecializedAgentStrategy _specializedAgentStrategy;
        private readonly IToolExecutionService _toolExecutionService;
        private readonly IAgentCommunicationService _agentCommService;
        private readonly ILogger<OrchestratorService> _logger;

        public OrchestratorService(
            ITaskDecomposer taskDecomposer,
            IProgressAnalyzer progressAnalyzer,
            IResultAggregator resultAggregator,
            IDependencyManager dependencyManager,
            IKernelFactory kernelFactory,
            ISpecializedAgentStrategy specializedAgentStrategy,
            IToolExecutionService toolExecutionService,
            IAgentCommunicationService agentCommService,
            ILogger<OrchestratorService> logger)
        {
            _taskDecomposer = taskDecomposer;
            _progressAnalyzer = progressAnalyzer;
            _resultAggregator = resultAggregator;
            _dependencyManager = dependencyManager;
            _kernelFactory = kernelFactory;
            _specializedAgentStrategy = specializedAgentStrategy;
            _toolExecutionService = toolExecutionService;
            _agentCommService = agentCommService;
            _logger = logger;
        }
        
        /// <summary>
        /// Orchestrates a complex task by decomposing, delegating, tracking, and aggregating results (event-driven, non-blocking).
        /// </summary>
        public async Task<OrchestratorResult> OrchestrateTaskAsync(AgentState agentState)
        {
            _logger.LogInformation("Orchestrating task: {Task}", agentState.Task);
            var agentConfig = agentState.Configuration;
            var kernel = _kernelFactory.CreateKernel(agentConfig);

            // 1. Decompose task
            var subTasks = await _taskDecomposer.DecomposeTaskAsync(agentState.Task, kernel);
            var orchestratorState =  agentState.Orchestrator ?? new OrchestratorState();
            orchestratorState.CurrentSubTasks = new List<SubTask>(subTasks);
            orchestratorState.ExecutionPlan = string.Join("\n", subTasks.Select(st => $"- {st.Task}"));
            orchestratorState.CreatedAt = DateTime.UtcNow;
            orchestratorState.LastUpdated = DateTime.UtcNow;

            // 2. For each subtask, create agentId, callId, register callback, delegate (non-blocking)
            foreach (var subTask in orchestratorState.CurrentSubTasks.Where(st => st.CanStart))
            {
                subTask.Status = SubTaskStatus.Delegated;
                // 1. 创建新 agent
                _ = Task.Run(async () => {
                    var childAgentId = await _agentCommService.CreateAgentAsync(agentState.Configuration!, agentState.AgentId);
                    subTask.ChildAgentId = childAgentId;
                    // 2. 生成 callId
                    var callId = Guid.NewGuid().ToString();
                    // 3. 注册 callback
                    orchestratorState.PendingCallbacks[callId] = new CallbackData
                    {
                        CallId = callId,
                        ChildAgentId = childAgentId,
                        Task = subTask.Task,
                        CreatedAt = DateTime.UtcNow
                    };
                    // 4. 通过 agentCommService 委派任务
                    await _agentCommService.CallAgentAsync(childAgentId, subTask.Task, callId, agentState.AgentId);
                });
            }

            // 3. Immediately return orchestration initiation result (not waiting for completion)
            return new OrchestratorResult
            {
                Success = false,
                CoordinatedAgents = orchestratorState.CurrentSubTasks.Select(st => AgentId.Parse(st.ChildAgentId ?? "simulated-agent")).ToList(),
                ExecutionPlan = orchestratorState.ExecutionPlan ?? string.Empty,
                EstimatedCompletionTime = TimeSpan.FromMinutes(5),
                ErrorMessages = new List<string>(),
                Strategy = "EventDrivenAsync",
                Metadata = new Dictionary<string, object> { { "SubTaskCount", orchestratorState.CurrentSubTasks.Count } }
            };
        }

        /// <summary>
        /// Process callback from child agent, update AgentState, dependencies, progress, and aggregation (event-driven).
        /// </summary>
        public async Task ProcessCallbackAsync(string callId, string message, bool isSuccess, AgentState agentState, Kernel kernel)
        {
            _logger.LogInformation("Processing callback {CallId} for orchestrator", callId);
            var orchestratorState =  agentState.Orchestrator ?? new OrchestratorState();
            // 1. Update callback data/state
            if (orchestratorState.PendingCallbacks.TryGetValue(callId, out var callbackData))
            {
                callbackData.IsReceived = true;
                callbackData.ResultMessage = message;
                callbackData.IsSuccess = isSuccess;
                callbackData.ReceivedAt = DateTime.UtcNow;
                orchestratorState.CompletedCallbacks.Add(new CompletedCallback
                {
                    CallId = callId,
                    ChildAgentId = callbackData.ChildAgentId,
                    Task = callbackData.Task,
                    ResultMessage = message,
                    IsSuccess = isSuccess,
                    CompletedAt = callbackData.ReceivedAt ?? DateTime.UtcNow,
                    ExecutionTime = (callbackData.ReceivedAt ?? DateTime.UtcNow) - callbackData.CreatedAt
                });
                orchestratorState.PendingCallbacks.Remove(callId);
            }
            // 2. Update dependencies
            _dependencyManager.UpdateDependencyResults(
                new SubTask { SubTaskId = callId, Task = message, Status = isSuccess ? SubTaskStatus.Completed : SubTaskStatus.Failed },
                message,
                orchestratorState.CurrentSubTasks);
            // 3. Analyze progress
            var completedCallbacks = orchestratorState.CompletedCallbacks;
            var decision = await _progressAnalyzer.AnalyzeProgressAsync(message, orchestratorState.CurrentSubTasks, completedCallbacks, kernel);
            _logger.LogInformation("Orchestration decision after callback: {Decision}", decision);
            // 4. If complete, aggregate results
            if (decision == OrchestrationDecision.CompleteTask)
            {
                var finalResult = await _resultAggregator.AggregateResultsAsync(message, completedCallbacks, kernel);
                _logger.LogInformation("Final aggregated result: {Result}", finalResult);
                // 可扩展为回调 parent agent
            }
            // 5. If new tasks available, delegate
            else if (decision == OrchestrationDecision.CreateAdditionalTasks)
            {
                var newTasks = _dependencyManager.CheckForNewlyAvailableTasks(orchestratorState.CurrentSubTasks);
                foreach (var subTask in newTasks)
                {
                    _ = Task.Run(async () => {
                        // 1. 创建新 agent
                        var newChildAgentId = await _agentCommService.CreateAgentAsync(agentState.Configuration!, agentState.AgentId);
                        subTask.ChildAgentId = newChildAgentId;
                        // 2. 生成 callId
                        var newCallId = Guid.NewGuid().ToString();
                        // 3. 注册 callback
                        orchestratorState.PendingCallbacks[newCallId] = new CallbackData
                        {
                            CallId = newCallId,
                            ChildAgentId = newChildAgentId,
                            Task = subTask.Task,
                            CreatedAt = DateTime.UtcNow
                        };
                        // 4. 通过 agentCommService 委派任务
                        await _agentCommService.CallAgentAsync(newChildAgentId, subTask.Task, newCallId, agentState.AgentId);
                    });
                }
            }
            // 6. If waiting, do nothing (event-driven)
        }

        /// <summary>
        /// Assigns a specific task to a target agent (stub for demo).
        /// </summary>
        public Task<bool> AssignTaskAsync(AgentId targetAgent, string taskDescription, IAgentContext context)
        {
            _logger.LogInformation("Assigning task '{Task}' to agent {AgentId}", taskDescription, targetAgent);
            // In real system, would delegate to agent
            return Task.FromResult(true);
        }

        public Task<OrchestratorResult> OrchestrateTaskAsync(string taskDescription, IAgentContext context)
        {
            throw new NotImplementedException("Use OrchestrateTaskAsync(AgentState) overload with full agent state.");
        }
    }
} 