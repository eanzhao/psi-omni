using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PsiOrleans.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace PsiOrleans.Plugins;

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
            _logger.LogInformation("Registered plugin: {PluginName} with {FunctionCount} functions", name, plugin.FunctionCount);
        }
        else
        {
            _logger.LogWarning("Plugin {PluginName} is already registered, skipping", name);
        }
    }

    public KernelFunction? GetFunction(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return _functions.TryGetValue(name, out var function) ? function : null;
    }

    public KernelPlugin? GetPlugin(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return _plugins.TryGetValue(name, out var plugin) ? plugin : null;
    }

    public IEnumerable<KernelFunction> GetFunctions(IEnumerable<string> names)
    {
        if (names == null)
            return Enumerable.Empty<KernelFunction>();
        var functions = new List<KernelFunction>();
        var notFound = new List<string>();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            if (_functions.TryGetValue(name, out var function))
            {
                functions.Add(function);
            }
            else
            {
                notFound.Add(name);
            }
        }
        if (notFound.Any())
        {
            _logger.LogWarning("Functions not found: {NotFoundFunctions}", string.Join(", ", notFound));
        }
        _logger.LogInformation("Retrieved {FoundCount} of {RequestedCount} functions", functions.Count, names.Count());
        return functions;
    }

    public IEnumerable<KernelPlugin> GetPlugins(IEnumerable<string> names)
    {
        if (names == null)
            return Enumerable.Empty<KernelPlugin>();
        var plugins = new List<KernelPlugin>();
        var notFound = new List<string>();
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            if (_plugins.TryGetValue(name, out var plugin))
            {
                plugins.Add(plugin);
            }
            else
            {
                notFound.Add(name);
            }
        }
        if (notFound.Any())
        {
            _logger.LogWarning("Plugins not found: {NotFoundPlugins}", string.Join(", ", notFound));
        }
        _logger.LogInformation("Retrieved {FoundCount} of {RequestedCount} plugins", plugins.Count, names.Count());
        return plugins;
    }

    public IEnumerable<string> GetAvailableFunctionNames() => _functions.Keys.ToList();
    public IEnumerable<string> GetAvailablePluginNames() => _plugins.Keys.ToList();

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

    public Dictionary<string, KernelFunction> GetToolsByQualifiedNames(IEnumerable<string> qualifiedNames)
    {
        var result = new Dictionary<string, KernelFunction>();
        var notFound = new List<string>();
        if (qualifiedNames == null)
            return result;
        foreach (var qualifiedName in qualifiedNames.Where(n => !string.IsNullOrWhiteSpace(n)))
        {
            var function = GetToolByQualifiedName(qualifiedName);
            if (function != null)
            {
                result[qualifiedName] = function;
            }
            else
            {
                notFound.Add(qualifiedName);
            }
        }
        if (notFound.Any())
        {
            _logger.LogWarning("Tools not found: {NotFoundTools}", string.Join(", ", notFound));
        }
        _logger.LogInformation("Retrieved {FoundCount} of {RequestedCount} tools", result.Count, qualifiedNames.Count());
        return result;
    }

    public IEnumerable<string> GetAllAvailableToolNames()
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