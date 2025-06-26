using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Common;

/// <summary>
/// Converts between PsiOrleans.Common.Models.ChatMessage and Semantic Kernel message types
/// </summary>
public static class ChatMessageConverter
{
    /// <summary>
    /// Converts a ChatMessage to a Semantic Kernel ChatMessageContent.
    /// </summary>
    /// <param name="message">The ChatMessage to convert.</param>
    /// <returns>A ChatMessageContent based on the input message.</returns>
    public static ChatMessageContent ToSemanticKernelMessage(ChatMessage message)
    {
        var role = message.Role.ToLowerInvariant() switch
        {
            "system" => AuthorRole.System,
            "user" => AuthorRole.User,
            "assistant" => AuthorRole.Assistant,
            "tool" => AuthorRole.Tool,
            _ => AuthorRole.User,
        };

        var metadata = new Dictionary<string, object?>();
        if (message.Metadata is { Count: > 0 })
        {
            foreach (var kvp in message.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        ChatMessageContent chatMessage;

        if (message.ToolCalls is { Count: > 0 })
        {
            var items = new ChatMessageContentItemCollection();
            if (!string.IsNullOrEmpty(message.Content))
            {
                items.Add(new TextContent(message.Content));
            }

            foreach (var toolCall in message.ToolCalls)
            {
                var kernelArgs = new KernelArguments();
                if (!string.IsNullOrEmpty(toolCall.FunctionArguments))
                {
                    try
                    {
                        var args = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.FunctionArguments);
                        if (args != null)
                        {
                            foreach (var arg in args)
                            {
                                kernelArgs[arg.Key] = arg.Value;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        kernelArgs["arguments"] = toolCall.FunctionArguments;
                    }
                }
                
                items.Add(new FunctionCallContent(toolCall.FunctionName, arguments: kernelArgs, id: Guid.NewGuid().ToString()));
            }

            chatMessage = new ChatMessageContent(role, items, metadata: metadata);
        }
        else
        {
            chatMessage = new ChatMessageContent(
                role: role,
                content: message.Content,
                metadata: metadata);
        }

#pragma warning disable SKEXP0001
        if (!string.IsNullOrEmpty(message.Name))
        {
            chatMessage.AuthorName = message.Name;
        }
#pragma warning restore SKEXP0001

        return chatMessage;
    }

    /// <summary>
    /// Converts a Semantic Kernel ChatMessageContent back to a serializable ChatMessage.
    /// </summary>
    /// <param name="skMessage">The Semantic Kernel message to convert.</param>
    /// <returns>A serializable ChatMessage based on the input message.</returns>
    public static ChatMessage FromSemanticKernelMessage(ChatMessageContent skMessage)
    {
        var psiMessage = new ChatMessage
        {
            Role = skMessage.Role.Label,
#pragma warning disable SKEXP0001
            Name = skMessage.AuthorName,
#pragma warning restore SKEXP0001
            Timestamp = DateTime.UtcNow, // SK message doesn't have a timestamp, so we set it here
            Content = skMessage.Content ?? string.Empty,
            Metadata = skMessage.Metadata?
                           .Where(kvp => kvp.Value is not null)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!)
                       ?? new Dictionary<string, object>()
        };

        var psiToolCalls = new List<ToolCall>();

        // First, try to get tool calls via reflection, in case it's an OpenAIChatMessageContent with a separate field.
        try
        {
            if (skMessage.GetType().FullName == "Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatMessageContent")
            {
                if (skMessage.GetType().GetProperty("ToolCalls")?.GetValue(skMessage) is System.Collections.IEnumerable skToolCalls)
                {
                    foreach (var skToolCall in skToolCalls)
                    {
                        var functionName = skToolCall.GetType().GetProperty("FunctionName")?.GetValue(skToolCall) as string;
                        var arguments = skToolCall.GetType().GetProperty("Arguments")?.GetValue(skToolCall);

                        if (functionName != null && arguments != null)
                        {
                            psiToolCalls.Add(new ToolCall
                            {
                                FunctionName = functionName,
                                FunctionArguments = JsonSerializer.Serialize(arguments)
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore reflection errors and fall back to the standard method.
        }

        // If reflection didn't yield tool calls, use the standard public Items collection.
        if (psiToolCalls.Count == 0 && skMessage.Items != null)
        {
            psiToolCalls.AddRange(skMessage.Items
                .OfType<FunctionCallContent>()
                .Select(fc => new ToolCall
                {
                    FunctionName = fc.FunctionName ?? string.Empty,
                    FunctionArguments = JsonSerializer.Serialize(fc.Arguments)
                }));
        }

        if (psiToolCalls.Count > 0)
        {
            psiMessage.ToolCalls = psiToolCalls;
        }

        return psiMessage;
    }
} 