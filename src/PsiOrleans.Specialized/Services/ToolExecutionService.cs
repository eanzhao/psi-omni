using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Specialized.Services;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PsiOrleans.Common.Models;
using PsiOrleans.Common.Interfaces;
using System.Collections.Generic;

namespace PsiOrleans.Specialized.Services
{
    public class ToolExecutionService : IToolExecutionService
    {
        public async Task<string> ExecuteAsync(string task, IKernelFactory factory, AgentState state, AgentConfiguration config, IEnumerable<string>? toolNames = null)
        {
            if (string.IsNullOrWhiteSpace(task))
                return "error: task is empty";

            var kernel = factory.CreateKernel(config, toolNames);
            if (kernel == null)
                throw new InvalidOperationException("Kernel is not configured for tool execution.");

            // 1. 获取 chat completion 服务
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            // 2. 构造 PromptExecutionSettings
            PromptExecutionSettings executionSettings;
            int maxTokens = 4000; // 默认最大 token
            double temperature = 0.1; // 默认温度
            // 只用 OpenAI 版本（无 config.Model 判断）
            executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = maxTokens,
                Temperature = temperature
            };

            // 3. 构造 ChatHistory
            var chatHistory = new ChatHistory();
            var enhancedPrompt = "You are a specialized agent. You MUST use the available tools.";
            chatHistory.AddSystemMessage(enhancedPrompt);
            var enhancedTask = task + "\n\nREQUIREMENT: Use the available tool functions. When you are done, summarize the result but do no more tool calls.";
            chatHistory.AddUserMessage(enhancedTask);

            // 4. 调用 LLM，自动工具调用
            var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);
            return result?.Content ?? "error: kernel returned null";
        }
    }

    // context 必须实现此接口以提供 Kernel
    // public interface IKernelContext : IToolContext
    // {
    //     Kernel Kernel { get; }
    // }
} 