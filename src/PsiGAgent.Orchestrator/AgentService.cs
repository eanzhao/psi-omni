using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiOrleans.Common;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;

namespace PsiGAgent.Orchestrator;

public interface IAgentService
{
    void SetConfiguration(AgentConfiguration agentConfiguration);
}

/// <summary>
/// Service providing agent creation capabilities as a KernelFunction
/// Can be used by any agent that needs to create new specialized agents
/// Now includes agent tracking and reuse capabilities
/// </summary>
public class AgentService : IAgentService
{
    private readonly IClusterClient _clusterClient;
    private readonly IKernelFunctionRegistry _functionRegistry;
    private IGAgentFactory _gAgentFactory;
    private readonly ILogger<AgentService> _logger;
    private ReaderWriterLock _configLock;
    private AgentConfiguration? _agentConfiguration;

    // Static registry to track all created agents across the system
    private static readonly ConcurrentDictionary<string, List<AgentParticulars>> CreatedAgents = new();

    public AgentService(
        IClusterClient clusterClient,
        IKernelFunctionRegistry functionRegistry,
        IGAgentFactory gAgentFactory,
        ILogger<AgentService> logger
    )
    {
        _clusterClient = clusterClient;
        _functionRegistry = functionRegistry;
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
    [KernelFunction("create_agent")]
    [Description(
        "Creates a new specialized agent with custom system prompt and specified tools. The LLM decides the prompt and tool selection.")]
    public async Task<string> CreateAgentAsync(
        [Description("The ID of the parent agent.")]
        string parentAgentId,
        [Description(
            "Comma-separated list of tool names this agent should have access to (e.g., 'Tavily.Search,Math.Add,Math.Divide')")]
        string toolNames
    )
    {
        try
        {
            _logger.LogInformation("🤖 Creating new agent with tools: {Tools}", toolNames);

            // Parse and validate tool names
            var requestedTools = toolNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToList();

            // Get available tools from registry
            var availableTools = _functionRegistry.GetAllAvailableToolNames().ToList();
            var validTools = requestedTools.Where(t => availableTools.Contains(t)).ToList();
            var invalidTools = requestedTools.Except(validTools).ToList();

            if (invalidTools.Any())
            {
                _logger.LogWarning("⚠️ Some requested tools are not available: {InvalidTools}",
                    string.Join(", ", invalidTools));
            }

            if (!validTools.Any())
            {
                return
                    $"❌ Error: None of the requested tools are available. Available tools: {string.Join(", ", availableTools.Take(10))}...";
            }


            // Create agent configuration with custom prompt
            var agentConfig = GetAgentConfiguration();
            if (agentConfig == null)
            {
                return
                    $"❌ Error: AgentConfiguration not set.";
            }

            // Create and initialize the new agent
            var psi = await _gAgentFactory.GetGAgentAsync("psi", "specialized");
            var agentId = psi.GetGrainId();
            // There's a publisher tied to each parent agent.
            var publisher = await GetPublisherAsync(parentAgentId);
            await publisher.PublishEventAsync(new AgentConfigEvent
                {
                    Configuration = agentConfig,
                    Tools = validTools
                },
                psi);

            var agentParticulars = new AgentParticulars
            {
                AgentId = agentId.ToString(),
                Tools = validTools
            };
            var myAgents = CreatedAgents.GetOrAdd(parentAgentId, () => new List<AgentParticulars>());
            myAgents.Add(agentParticulars);
            return JsonSerializer.Serialize(agentParticulars);
        }
        catch (Exception ex)
        {
            var errorMessage = $"❌ Error creating agent with tools {toolNames}: {ex.Message}";
            _logger.LogError(ex, "❌ Error creating agent with tools {ToolNames}", toolNames);
            return errorMessage;
        }
    }

    /// <summary>
    /// Get all available tools from the function registry
    /// </summary>
    [KernelFunction("list_available_tools")]
    [Description("Lists all available tools that can be assigned to new agents")]
    public async Task<string> ListAvailableToolsAsync()
    {
        try
        {
            var availableTools = _functionRegistry.GetAllAvailableToolNames().ToList();

            var result = $@"📋 Available Tools for Agent Creation:
Total: {availableTools.Count} tools

Tools:
{string.Join("\n", availableTools.Select(t => $"- {t}"))}

Usage: Use these tool names in the create_agent function's toolNames parameter.";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error listing available tools");
            return $"❌ Error listing tools: {ex.Message}";
        }
    }

    /// <summary>
    /// List all created agents with their metadata
    /// </summary>
    [KernelFunction("list_created_agents")]
    [Description("Lists all agents that have been created by the system with their usage statistics")]
    public async Task<string> ListCreatedAgentsAsync(
        [Description("The ID of the parent agent.")]
        string parentAgentId
    )
    {
        try
        {
            if (!CreatedAgents.Any())
            {
                return "📋 No agents have been created yet by the system.";
            }

            var agents = CreatedAgents[parentAgentId];

            var result = $@"📋 Created Agents Registry:
Total: {agents.Count} agents

Agents (sorted by usage):
{string.Join("\n", agents.Select(a => $"- {a}"))}

You can reuse these agents by calling them with call_agent using their Agent ID.";

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
        [Description("The unique ID of the agent to call.")]
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

//     /// <summary>
//     /// Find suitable existing agents for a given task
//     /// </summary>
//     [KernelFunction("find_suitable_agent")]
//     [Description("Finds existing created agents that might be suitable for handling a specific task")]
//     public async Task<string> FindSuitableAgentAsync(
//         [Description("Description of the task to find a suitable agent for")]
//         string taskDescription
//     )
//     {
//         try
//         {
//             if (!_createdAgents.Any())
//             {
//                 return "🔍 No created agents available. You may need to create a new agent for this task.";
//             }
//
//             var suitableAgents = _createdAgents.Values
//                 .Where(a => a.MightHandleTask(taskDescription))
//                 .OrderByDescending(a => a.UsageCount)
//                 .ToList();
//
//             if (!suitableAgents.Any())
//             {
//                 var allAgents = string.Join(", ", _createdAgents.Values.Select(a => $"{a.AgentName} ({a.AgentId})"));
//                 return $@"🔍 No suitable existing agents found for task: '{taskDescription}'
//
// Available agents: {allAgents}
//
// Consider creating a new specialized agent for this task.";
//             }
//
//             var bestMatch = suitableAgents.First();
//             var alternatives = suitableAgents.Skip(1).Take(2).ToList();
//
//             var result = $@"🎯 Found suitable agent for task: '{taskDescription}'
//
// Best Match: {bestMatch.AgentName} ({bestMatch.AgentId})
// - Specialization: {bestMatch.Specialization}
// - Usage Count: {bestMatch.UsageCount}
// - Tools: {string.Join(", ", bestMatch.Tools)}";
//
//             if (alternatives.Any())
//             {
//                 result += $@"
//
// Alternative options:
// {string.Join("\n", alternatives.Select(a => $"- {a.AgentName} ({a.AgentId}) - Used {a.UsageCount} times"))}";
//             }
//
//             result += $"\n\nRecommendation: Use call_agent with agent ID '{bestMatch.AgentId}' to execute this task.";
//
//             return result;
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "❌ Error finding suitable agent");
//             return $"❌ Error finding suitable agent: {ex.Message}";
//         }
//     }
}