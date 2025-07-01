using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PsiGAgent.Common.Interfaces;

namespace PsiGAgent.Plugins;

public class KernelFunctionRegistry : IKernelFunctionRegistry
{
    private readonly ConcurrentDictionary<string, KernelFunction> _functions = new();
    private readonly ConcurrentDictionary<string, KernelPlugin> _plugins = new();
    private readonly ILogger<KernelFunctionRegistry> _logger;

    public KernelFunctionRegistry(ILogger<KernelFunctionRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterFunction(string name, KernelFunction function)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Function name cannot be null or empty", nameof(name));
        if (function == null)
            throw new ArgumentNullException(nameof(function));
        if (_functions.TryAdd(name, function))
        {
            _logger.LogInformation("Registered function: {FunctionName}", name);
        }
        else
        {
            _logger.LogWarning("Function {FunctionName} is already registered, skipping", name);
        }
    }

    public void RegisterPlugin(string name, KernelPlugin plugin)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Plugin name cannot be null or empty", nameof(name));
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));
        if (_plugins.TryAdd(name, plugin))
        {
            _logger.LogInformation("Registered plugin: {PluginName} with {FunctionCount} functions", name,
                plugin.FunctionCount);
        }
        else
        {
            _logger.LogWarning("Plugin {PluginName} is already registered, skipping", name);
        }
    }

    public KernelFunction? GetToolByQualifiedName(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            return null;
        if (_functions.TryGetValue(qualifiedName, out var individualFunction))
        {
            return individualFunction;
        }

        var parts = qualifiedName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2)
        {
            var pluginName = parts[0];
            var functionName = parts[1];
            if (_plugins.TryGetValue(pluginName, out var plugin))
            {
                return plugin.TryGetFunction(functionName, out var pluginFunction) ? pluginFunction : null;
            }
        }

        return null;
    }

    
    // public static FunctionToolDefinition ToToolDefinition(this KernelFunction function, string? pluginName = null)
    // {
    //     if (function.Metadata.Parameters.Count > 0)
    //     {
    //         BinaryData parameterData = function.Metadata.CreateParameterSpec();
    //
    //         return new FunctionToolDefinition(FunctionName.ToFullyQualifiedName(function.Name, pluginName ?? function.PluginName))
    //         {
    //             Description = function.Description,
    //             Parameters = parameterData,
    //         };
    //     }
    //
    //     return new FunctionToolDefinition(FunctionName.ToFullyQualifiedName(function.Name, pluginName ?? function.PluginName))
    //     {
    //         Description = function.Description
    //     };
    // }
    
    // public List<FunctionToolDefinition> GetAllToolDefinitions()
    // {
    //     return _functions.Values.Select(f => new { Function = f, PluginName = string.Empty }).Concat(
    //         _plugins.Values.SelectMany(p => p.Select(f => new { Function = f, PluginName = p.Name }))
    //     ).Select(item =>
    //     {
    //         if (string.IsNullOrEmpty(item.PluginName))
    //         {
    //             return item.Function.ToToolDefinition();
    //         }
    //         else
    //         {
    //             return item.Function.ToToolDefinition();
    //         }
    //     }).ToList();
    // }

    public List<string> GetAllAvailableToolNames()
    {
        var toolNames = new List<string>();
        toolNames.AddRange(_functions.Keys);
        foreach (var plugin in _plugins.Values)
        {
            foreach (var function in plugin)
            {
                toolNames.Add($"{plugin.Name}.{function.Name}");
            }
        }

        return toolNames.OrderBy(name => name).ToList();
    }
    
    
}