using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Interfaces;
using PsiOrleans.Common.Models;
using PsiOrleans.Plugins;

namespace PsiOrleans.Analysis.Services;

/// <summary>
/// Core task analysis service that determines whether tasks should be handled by 
/// ORCHESTRATOR mode (complex, needs decomposition) or SPECIALIZED mode (direct execution).
/// Implements the same simple LLM-based analysis as the original ConfigurableAgentGrain.
/// </summary>
public class TaskAnalyzer : ITaskAnalyzer
{
    private readonly IKernelFactory _factory;

    public TaskAnalyzer(IKernelFactory factory)
    {
        _factory = factory;
    }
    
    /// <summary>
    /// Analyzes a task to determine its complexity and recommended agent role.
    /// Uses the same LLM prompt logic as the original ConfigurableAgentGrain.AnalyzeTaskAndDetermineRoleAsync.
    /// </summary>
    public async Task<TaskAnalysisResult> AnalyzeTaskAsync(AgentState state)
    {
        if (string.IsNullOrWhiteSpace(state.Task))
            throw new ArgumentException("Task description cannot be null or empty", nameof(state.Task));

        if (state.Configuration == null)
            throw new ArgumentNullException(nameof(state.Configuration));

        // Validate API key before proceeding
        if (string.IsNullOrEmpty(state.Configuration.Model.ApiKey))
            throw new InvalidOperationException("API key is required for task analysis");

        var configuration = state.Configuration;
        var taskDescription = state.Task;
        
        try
        {
            var kernel = _factory.CreateKernel(state.Configuration);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            // 获取所有可用工具名和描述
            var toolInfos = new List<string>();
            var registry = _factory.FunctionRegistry;
            if (registry != null)
            {
                foreach (var name in registry.GetAvailableFunctionNames())
                {
                    var func = registry.GetFunction(name);
                    var desc = func?.Description?.Replace("\"", "'") ?? "No description";
                    toolInfos.Add($"{{\"name\":\"{name}\",\"description\":\"{desc}\"}}");
                }
            }
            var toolListJson = "[" + string.Join(",", toolInfos) + "]";

            var analysisPrompt =
                "Analyze this task and determine if it should be handled by an ORCHESTRATOR or SPECIALIZED agent.\n" +
                $"Task: {taskDescription}\n" +
                $"Available tools (with descriptions): {toolListJson}\n" +
                "ORCHESTRATOR agents should handle tasks that:\n" +
                "- Require breaking down into multiple subtasks\n" +
                "- Need coordination between different capabilities\n" +
                "- Involve complex multi-step workflows\n" +
                "- Require delegation and result aggregation\n" +
                "SPECIALIZED agents should handle tasks that:\n" +
                "- Can be completed with direct tool usage\n" +
                "- Are focused and specific\n" +
                "- Don't require task decomposition\n" +
                "- Can be solved with available functions\n" +
                "Respond in the following JSON format:\n{\n  \"role\": \"ORCHESTRATOR\" or \"SPECIALIZED\",\n  \"recommended_tools\": [list of tool names from available tools]\n}\n" +
                "When choosing tools, use both the name and the description to decide which are most relevant.";

            // Execute LLM analysis
            var result = await chatService.GetChatMessageContentAsync(analysisPrompt);
            var response = result.Content?.Trim() ?? "{\"role\":\"SPECIALIZED\",\"recommended_tools\":[]}";

            // 解析 JSON 响应
            var role = "SPECIALIZED";
            var recommendedTools = new List<string>();
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(StripMarkdownJsonBlock(response));
                if (json.RootElement.TryGetProperty("role", out var roleProp))
                    role = roleProp.GetString()?.ToUpperInvariant() ?? "SPECIALIZED";
                if (json.RootElement.TryGetProperty("recommended_tools", out var toolsProp) && toolsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    recommendedTools = toolsProp.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }
            catch { /* fallback to default */ }

            var isOrchestrator = role.Contains("ORCHESTRATOR");

            return new TaskAnalysisResult
            {
                RecommendedApproach = isOrchestrator ? TaskApproach.Orchestration : TaskApproach.DirectExecution,
                CanBeDecomposed = isOrchestrator,
                AnalysisNotes = $"LLM analysis result: {response}",
                RecommendedTools = recommendedTools
            };
        }
        catch (Exception ex)
        {
            // Default to SPECIALIZED on error (same as original)
            return new TaskAnalysisResult
            {
                RecommendedApproach = TaskApproach.DirectExecution,
                CanBeDecomposed = false,
                AnalysisNotes = $"Fallback to SPECIALIZED mode due to error: {ex.Message}"
            };
        }
    }

    private static string StripMarkdownJsonBlock(string input)
    {
        var s = input.Trim();
        if (s.StartsWith("```json"))
            s = s.Substring(7).TrimStart();
        else if (s.StartsWith("```"))
            s = s.Substring(3).TrimStart();
        if (s.EndsWith("```"))
            s = s.Substring(0, s.Length - 3).TrimEnd();
        return s;
    }
} 