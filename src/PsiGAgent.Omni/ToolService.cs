using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiOrleans.Common.Interfaces;

namespace PsiAgnet.Omni;

public interface IToolService
{
}

public class ToolService : IToolService
{
    private readonly IKernelFunctionRegistry _functionRegistry;
    private readonly ILogger<ToolService> _logger;

    public ToolService(IKernelFunctionRegistry functionRegistry, ILogger<ToolService> logger)
    {
        _functionRegistry = functionRegistry;
        _logger = logger;
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
}