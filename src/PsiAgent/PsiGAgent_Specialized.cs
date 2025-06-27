using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PsiOrleans.Common;

namespace PsiAgent;

public partial class PsiGAgent
{
    private async Task<ChatHistory?> ExecuteSpecializedAsync()
    {
        if (string.IsNullOrEmpty(State.Task))
        {
            // Do nothing
            return null;
        }

        if (State.Configuration == null)
        {
            // Do nothing
            return null;
        }

        var kernel = _kernelFactory.CreateKernel(
            State.Configuration,
            State.TaskAnalysisResult.RecommendedTools
        );
        if (kernel == null)
            throw new InvalidOperationException("Kernel is not configured for tool execution.");

        // 1. 获取 chat completion 服务
        var chatService = kernel.GetRequiredService<IChatCompletionService>();


        // 2. 构造 PromptExecutionSettings
        var maxTokens = 4000; // 默认最大 token
        var temperature = 0.1; // 默认温度
        // 只用 OpenAI 版本（无 config.Model 判断）
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = maxTokens,
            Temperature = temperature
        };

        var chatHistory = GetChatHistory();
        // var enhancedPrompt = "You are a specialized agent. You MUST use the available tools.";
        // chatHistory.AddSystemMessage(enhancedPrompt);
        // var enhancedTask = State.Task +
        //                    "\n\nREQUIREMENT: Use the available tool functions. When you are done, summarize the result but do no more tool calls.";
        // chatHistory.AddUserMessage(enhancedTask);
        var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
        chatHistory.Add(result);
        // Logger.LogInformation($"result: {result}");
        return chatHistory;
    }

    private ChatHistory GetChatHistory()
    {
        var chatHistory = new ChatHistory();
        var messages =
            State.SpecializedState.ChatHistory.Select(message => ChatMessageConverter.ToSemanticKernelMessage(message));
        chatHistory.AddRange(messages);

        return chatHistory;
    }
}