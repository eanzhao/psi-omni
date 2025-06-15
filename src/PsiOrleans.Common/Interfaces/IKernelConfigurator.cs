using Microsoft.SemanticKernel;

namespace PsiOrleans.Common.Interfaces;

public interface IKernelConfigurator
{
    void Configure(IKernelBuilder builder);
}