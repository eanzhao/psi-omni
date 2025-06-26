using System;
using System.Collections.Generic;
using System.Text;
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
    /// Converts a ChatMessage to a Semantic Kernel ChatMessageContent
    /// </summary>
    /// <param name="message">The ChatMessage to convert</param>
    /// <returns>A ChatMessageContent based on the input message</returns>
    public static ChatMessageContent ToSemanticKernelMessage(ChatMessage message)
    {
        // Convert role string to AuthorRole enum
        AuthorRole role = message.Role.ToLowerInvariant() switch
        {
            "system" => AuthorRole.System,
            "user" => AuthorRole.User,
            "assistant" => AuthorRole.Assistant,
            "tool" => AuthorRole.Tool,
            _ => AuthorRole.User // Default to User for unknown roles
        };
        
        // Create a new ChatMessageContent
        ChatMessageContent chatMessage;
        
        // Create a metadata dictionary
        var metadata = new Dictionary<string, object?>();
        if (message.Metadata != null && message.Metadata.Count > 0)
        {
            foreach (var kvp in message.Metadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }
        
        if (message.ToolCalls != null && message.ToolCalls.Count > 0)
        {
            // Create a message with tool calls
            var items = new ChatMessageContentItemCollection();
            
            // Add the text content if available
            if (!string.IsNullOrEmpty(message.Content))
            {
                items.Add(new TextContent(message.Content));
            }
            
            // Add tool calls
            foreach (var toolCall in message.ToolCalls)
            {
                // Create a function call content for each tool call
                // Create arguments as KernelArguments
                var kernelArgs = new KernelArguments();
                try
                {
                    // Try to parse the arguments as JSON
                    var args = JsonSerializer.Deserialize<Dictionary<string, object>>(toolCall.FunctionArguments);
                    if (args != null)
                    {
                        foreach (var arg in args)
                        {
                            kernelArgs.Add(arg.Key, arg.Value);
                        }
                    }
                }
                catch
                {
                    // If parsing fails, add the raw string as a single argument
                    kernelArgs.Add("arguments", toolCall.FunctionArguments);
                }
                
                items.Add(new FunctionCallContent(
                    functionName: toolCall.FunctionName,
                    arguments: kernelArgs,
                    id: Guid.NewGuid().ToString()
                ));
            }
            
            // Create the chat message with items collection
            chatMessage = new ChatMessageContent(
                role: role,
                items,
                innerContent: null,
                encoding: null,
                metadata: metadata
            );
        }
        else
        {
            // Create a simple message with just content
            chatMessage = new ChatMessageContent(
                role: role,
                content: message.Content,
                innerContent: null,
                encoding: null,
                metadata: metadata
            );
        }
        
        // Set the author name if provided
        #pragma warning disable SKEXP0001
        if (!string.IsNullOrEmpty(message.Name))
        {
            chatMessage.AuthorName = message.Name;
        }
        #pragma warning restore SKEXP0001
        
        return chatMessage;
    }
} 