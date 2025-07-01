using Microsoft.SemanticKernel;

namespace PsiGAgent.Common.Interfaces;

public interface IKernelFunctionRegistry
{
    void RegisterFunction(string name, KernelFunction function);
    void RegisterPlugin(string name, KernelPlugin plugin);
    KernelFunction? GetToolByQualifiedName(string qualifiedName);
    List<string> GetAllAvailableToolNames();
} 