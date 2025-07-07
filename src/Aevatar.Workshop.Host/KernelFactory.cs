using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using PsiGAgent.Common.Interfaces;
using PsiGAgent.Common.Models;

namespace Aevatar.Workshop.Host;

public class KernelFactory : IKernelFactory
{
    public KernelFactory(IKernelFunctionRegistry kernelFunctionRegistry)
    {
        FunctionRegistry = kernelFunctionRegistry;
    }

    public Kernel CreateKernel(AgentConfiguration configuration, IEnumerable<string>? toolNames = null)
    {
        // TODO: validate configuration
        // Create a kernel for LLM analysis (same approach as original)
        var kernelBuilder = Kernel.CreateBuilder();

        // Add chat completion service based on configuration
        if (configuration.Model.IsAzureOpenAI)
        {
            kernelBuilder.AddAzureOpenAIChatCompletion(
                configuration.Model.DeploymentName ?? configuration.Model.ModelId,
                configuration.Model.Endpoint!,
                configuration.Model.ApiKey);
        }
        else
        {
            // Check if we have a custom base URL (e.g., for DeepSeek or other OpenAI-compatible APIs)
            if (!string.IsNullOrEmpty(configuration.Model.BaseUrl))
            {
                // Use OpenAI client with custom base URL
                var httpClient = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri(configuration.Model.BaseUrl)
                };
                
                kernelBuilder.AddOpenAIChatCompletion(
                    configuration.Model.ModelId,
                    configuration.Model.ApiKey,
                    httpClient: httpClient);
            }
            else
            {
                // Standard OpenAI
                kernelBuilder.AddOpenAIChatCompletion(
                    configuration.Model.ModelId,
                    configuration.Model.ApiKey);
            }
        }


        if (toolNames != null)
        {
            var funcs = new List<KernelFunction>();
            foreach (var toolName in toolNames)
            {
                var func = FunctionRegistry.GetToolByQualifiedName(toolName);
                if (func != null)
                {
                    funcs.Add(func);
                }
            }

            if (funcs.Count > 0)
            {
                var mathPlugin = KernelPluginFactory.CreateFromFunctions("Tools", funcs);
                kernelBuilder.Plugins.Add(mathPlugin);
            }
        }

        return kernelBuilder.Build();
    }

    public IKernelFunctionRegistry? FunctionRegistry { get; }
}