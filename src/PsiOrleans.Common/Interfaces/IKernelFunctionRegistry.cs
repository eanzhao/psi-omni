using Microsoft.SemanticKernel;
using System.Collections.Generic;

namespace PsiOrleans.Common.Interfaces;

public interface IKernelFunctionRegistry
{
    void RegisterFunction(string name, KernelFunction function);
    void RegisterPlugin(string name, KernelPlugin plugin);
    KernelFunction? GetToolByQualifiedName(string qualifiedName);
    List<string> GetAllAvailableToolNames();
} 