using Microsoft.SemanticKernel;
using PsiGAgent.Common.Models;

namespace PsiGAgent.Common.Interfaces;

public interface IKernelFactory
{
    Kernel CreateKernel(AgentConfiguration configuration, IEnumerable<string>? toolNames = null);
    IKernelFunctionRegistry? FunctionRegistry { get; }
}