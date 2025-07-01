using System.Text.Json;
using Microsoft.SemanticKernel;
using PsiOrleans.Common.Interfaces;

namespace PsiGAgent.Omni;

public static class Extensions
{
    public static List<JsonElement> GetAllToolDefinitions(this IKernelFunctionRegistry kernelFunctionRegistry)
    {
        return (from toolName in kernelFunctionRegistry.GetAllAvailableToolNames()
            let kernelFunction = kernelFunctionRegistry.GetToolByQualifiedName(toolName)!
            select JsonSerializer.SerializeToDocument(kernelFunction.ToToolDefinition()).RootElement.Clone()).ToList();
    }

    public static ToolDefinition ToToolDefinition(this KernelFunction kernelFunction)
    {
        var toolName = kernelFunction.PluginName.IsNullOrEmpty()
            ? kernelFunction.Name
            : $"{kernelFunction.PluginName}.{kernelFunction.Name}";
        return new ToolDefinition
        {
            Name = toolName,
            Description = kernelFunction.Description,
            Parameters = kernelFunction.Metadata.Parameters.Select(p => new ToolParameter
            {
                Name = p.Name,
                Description = p.Description,
                IsRequired = p.IsRequired,
                Schema = p.Schema?.RootElement.Clone().ToString() ?? string.Empty
            }).ToList()
        };
    }
}