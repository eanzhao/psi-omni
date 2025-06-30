using System.Text.Json;
using PsiOrleans.Common.Interfaces;

namespace PsiGAgent.Omni;

public static class Extensions
{
    public static List<JsonElement> GetAllToolDefinitions(this IKernelFunctionRegistry kernelFunctionRegistry)
    {
        return (from toolName in kernelFunctionRegistry.GetAllAvailableToolNames()
            let kernelFunction = kernelFunctionRegistry.GetToolByQualifiedName(toolName)!
            select JsonSerializer.SerializeToDocument(new
            {
                Name = toolName,
                Description = kernelFunction.Description,
                Parameters = kernelFunction.Metadata.Parameters.Select(p => new
                {
                    Name = p.Name,
                    Description = p.Description,
                    IsRequired = p.IsRequired,
                    Schema = p.Schema.RootElement.Clone()
                }).ToList()
            }).RootElement.Clone()).ToList();
    }

    public static JsonElement ToJsonElement(this ToolDefinition toolDefinition)
    {
        var tempObject = new
        {
            toolDefinition.Name,
            toolDefinition.Description,
            Parameters = toolDefinition.Parameters.Select(p => JsonDocument.Parse(p).RootElement.Clone()).ToList()
        };
        return JsonSerializer.SerializeToDocument(tempObject).RootElement.Clone();
    }
}