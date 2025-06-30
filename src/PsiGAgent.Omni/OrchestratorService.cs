using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiOrleans.Common;
using PsiOrleans.Common.Models;

namespace PsiGAgent.Omni;

public interface IOrchestratorService
{
    void SetConfiguration(AgentConfiguration agentConfiguration);
    void UpdateChildAgents(string parentAgentId, List<AgentDescriptor> childAgents);
}

/// <summary>
/// Service providing agent creation capabilities as a KernelFunction
/// Can be used by any agent that needs to create new specialized agents
/// Now includes agent tracking and reuse capabilities
/// </summary>
public class OrchestratorService : IOrchestratorService
{
    private IGAgentFactory _gAgentFactory;
    private readonly ILogger<OrchestratorService> _logger;
    private ReaderWriterLock _configLock;
    private AgentConfiguration? _agentConfiguration;

    // Static registry to track all created agents across the system
    private static readonly ConcurrentDictionary<string, List<AgentDescriptor>> ChildrenRegistry = new();

    public OrchestratorService(
        IGAgentFactory gAgentFactory,
        ILogger<OrchestratorService> logger
    )
    {
        _logger = logger;
        _gAgentFactory = gAgentFactory;
        _configLock = new ReaderWriterLock();
    }

    public void SetConfiguration(AgentConfiguration agentConfiguration)
    {
        _configLock.AcquireWriterLock(500);
        _agentConfiguration ??= agentConfiguration;
        _configLock.ReleaseWriterLock();
    }

    public void UpdateChildAgents(string parentAgentId, List<AgentDescriptor> childAgents)
    {
        ChildrenRegistry.AddOrUpdate(parentAgentId, childAgents, (key, val) => childAgents);
    }

    private AgentConfiguration? GetAgentConfiguration()
    {
        _configLock.AcquireReaderLock(500);
        var config = _agentConfiguration;
        _configLock.ReleaseReaderLock();
        return config;
    }

    /// <summary>
    /// Create a new specialized agent with custom prompt and tools
    /// </summary>
    [KernelFunction("create_and_call_agent")]
    [Description("Creates a new agent with an initial task. The result will be notified to the given parent agent. Don't use call_agent to send the task again.")]
    public async Task<string> CreateAgentAsync(
        [Description("The ID of the parent agent.")]
        string parentAgentId,
        [Description("The call ID of this call.")]
        string callId,
        [Description("The description of the task sent to the new agent. Please provide all necessary information.")]
        string task
    )
    {
        try
        {
            // Create agent configuration with custom prompt
            var agentConfig = GetAgentConfiguration();
            if (agentConfig == null)
            {
                return
                    $"❌ Error: AgentConfiguration not set.";
            }

            // Create and initialize the new agent
            var psi = await _gAgentFactory.GetGAgentAsync("psi", "omni");
            var agentId = psi.GetGrainId();
            // There's a publisher tied to each parent agent.
            var publisher = await GetPublisherAsync(parentAgentId);
            await publisher.PublishEventAsync(new AgentConfigEvent
                {
                    Configuration = agentConfig,
                    ParentAgentId = parentAgentId
                },
                psi);

            await publisher.PublishEventAsync(new UserMessageEvent
            {
                TargetAgentId = agentId.ToString(),
                CallId = callId,
                Content = task,
                ReplyToAgentId = parentAgentId
            }, psi);
            var descriptor = new AgentDescriptor
            {
                AgentId = agentId.ToString(),
                AgentType = string.Empty,
                Description = string.Empty,
                Examples = new List<AgentExample>()
                {
                    new AgentExample
                    {
                        Request = task,
                        Response = string.Empty
                    }
                },
                Tools = new List<string>()
            };

            if (!ChildrenRegistry.TryGetValue(parentAgentId, out var children))
            {
                children = new List<AgentDescriptor>();
            }

            children.Add(descriptor);

            return $"Created the following agent and sent the subtask ${callId} to it:\n{JsonSerializer.Serialize(descriptor)}";
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ Error creating agent for parent {parentAgentId}: {ex.Message}";
            _logger.LogError(ex, "❌ Error creating agent for parent {Parent}", parentAgentId);
            return errorMessage;
        }
    }

    /// <summary>
    /// List all created agents with their metadata
    /// </summary>
    [KernelFunction("list_child_agents")]
    [Description("Lists all child agents for this agent that is acting as an orchestrator.")]
    public async Task<string> ListChildAgentsAsync(
        [Description("The ID of the parent agent.")]
        string parentAgentId
    )
    {
        try
        {
            if (!ChildrenRegistry.Any() || !ChildrenRegistry.ContainsKey(parentAgentId))
            {
                return "No agents have been created.";
            }

            var agents = ChildrenRegistry[parentAgentId];

            var result = $@"📋 Created Agents Registry:
Total: {agents.Count} agents

Child Agents Details:
{string.Join("\n", agents.Select(a => JsonSerializer.Serialize(a)))}
";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error listing created agents");
            return $"❌ Error listing created agents: {ex.Message}";
        }
    }

    [KernelFunction("call_agent")]
    [Description("Calls any ConfigurableAgentGrain by its ID with a natural language query")]
    public async Task<string> CallAgentAsync(
        [Description("The unique ID of the parent agent making the call.")]
        string parentAgentId,
        [Description("The unique ID of the agent to call."), Required]
        string agentId,
        [Description("The call ID of this call.")]
        string callId,
        [Description("Natural language message to be sent to the agent")]
        string message
    )
    {
        _logger.LogInformation("🔗 Generic agent proxy called for {AgentId} with message: {Message}", agentId, message);

        try
        {
            var publisher = await GetPublisherAsync(parentAgentId);
            var targetAgent = await _gAgentFactory.GetGAgentAsync(GrainId.Parse(agentId));

            await publisher.PublishEventAsync(new UserMessageEvent
            {
                TargetAgentId = agentId,
                CallId = callId,
                Content = message,
                ReplyToAgentId = parentAgentId
            }, targetAgent);
            return $"Message sent for callId: {callId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in generic agent proxy for {AgentId}", agentId);
            return $"Error calling agent '{agentId}': {ex.Message}";
        }
    }

    private async Task<IPublishingGAgent> GetPublisherAsync(string parentAgentId)
    {
        var id = Guid.Parse(parentAgentId.Split("/").Last());
        var publisher = await _gAgentFactory.GetGAgentAsync<IPublishingGAgent>(id);
        return publisher;
    }
}