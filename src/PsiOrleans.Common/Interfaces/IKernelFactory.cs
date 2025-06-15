using Microsoft.SemanticKernel;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces;

public interface IKernelFactory
{
    Kernel CreateKernel(AgentConfiguration configuration, IEnumerable<string>? toolNames = null);
    IKernelFunctionRegistry? FunctionRegistry { get; }
}