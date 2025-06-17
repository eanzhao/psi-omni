using Microsoft.SemanticKernel;
using System.Collections.Generic;

namespace PsiOrleans.Common.Interfaces;

public interface IKernelFunctionRegistry
{
    void RegisterFunction(string name, KernelFunction function);
    void RegisterPlugin(string name, KernelPlugin plugin);
    KernelFunction? GetFunction(string name);
    KernelPlugin? GetPlugin(string name);
    IEnumerable<KernelFunction> GetFunctions(IEnumerable<string> names);
    IEnumerable<KernelPlugin> GetPlugins(IEnumerable<string> names);
    IEnumerable<string> GetAllFunctionNames();
    IEnumerable<string> GetAvailableFunctionNames();
    IEnumerable<string> GetAvailablePluginNames();
    KernelFunction? GetToolByQualifiedName(string qualifiedName);
    Dictionary<string, KernelFunction> GetToolsByQualifiedNames(IEnumerable<string> qualifiedNames);
    IEnumerable<string> GetAllAvailableToolNames();
} 