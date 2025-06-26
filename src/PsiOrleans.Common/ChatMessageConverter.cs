using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using ChatMessage = PsiOrleans.Common.Models.ChatMessage;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;
using PsiOrleans.Common.Models;

namespace PsiOrleans.Common;

/// <summary>
/// Custom implementation of a tool call for OpenAI
/// </summary>
public class CustomChatToolCall
{
    /// <summary>
    /// Gets the unique identifier for this tool call
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// Gets the function name
    /// </summary>
    public string FunctionName { get; }
    
    /// <summary>
    /// Gets the function arguments as a JSON string
    /// </summary>
    public string FunctionArguments { get; }
    
    /// <summary>
    /// Creates a new instance of the CustomChatToolCall class
    /// </summary>
    public CustomChatToolCall(string id, string functionName, string functionArguments)
    {
        Id = id;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }
}

/// <summary>
/// Converts between PsiOrleans.Common.Models.ChatMessage and Semantic Kernel message types
/// </summary>
public static class ChatMessageConverter
{
    // Add a static field to track whether reflection succeeded
    public static bool ReflectionSucceeded { get; private set; } = false;
    public static string? LastError { get; private set; }
    
    /// <summary>
    /// Converts a ChatMessage to a Semantic Kernel ChatMessageContent or OpenAIChatMessageContent
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
            
            // Create a list to store custom tool calls
            var toolCalls = new List<CustomChatToolCall>();
            
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
                
                // Add to regular items collection
                var functionCallContent = new FunctionCallContent(
                    functionName: toolCall.FunctionName,
                    arguments: kernelArgs,
                    id: Guid.NewGuid().ToString()
                );
                items.Add(functionCallContent);
                
                // Create custom tool call
                var customToolCall = new CustomChatToolCall(
                    Guid.NewGuid().ToString(), 
                    toolCall.FunctionName, 
                    toolCall.FunctionArguments
                );
                toolCalls.Add(customToolCall);
            }
            
            // Try to create an OpenAIChatMessageContent
            var openAiMessage = CreateOpenAIChatMessageContent(role, message.Content, toolCalls, metadata);
            if (openAiMessage != null)
            {
                // Set the author name if provided
                #pragma warning disable SKEXP0001
                if (!string.IsNullOrEmpty(message.Name))
                {
                    openAiMessage.AuthorName = message.Name;
                }
                #pragma warning restore SKEXP0001
                
                return openAiMessage;
            }
            
            // Fallback to regular ChatMessageContent if OpenAIChatMessageContent creation fails
            var chatMessage = new ChatMessageContent(
                role: role,
                items,
                innerContent: null,
                encoding: null,
                metadata: metadata
            );
            
            // Set the author name if provided
            #pragma warning disable SKEXP0001
            if (!string.IsNullOrEmpty(message.Name))
            {
                chatMessage.AuthorName = message.Name;
            }
            #pragma warning restore SKEXP0001
            
            return chatMessage;
        }
        else
        {
            // Create a simple message with just content
            var chatMessage = new ChatMessageContent(
                role: role,
                content: message.Content,
                innerContent: null,
                encoding: null,
                metadata: metadata
            );
            
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
    
    /// <summary>
    /// Creates an OpenAIChatMessageContent using reflection
    /// </summary>
    private static ChatMessageContent? CreateOpenAIChatMessageContent(
        AuthorRole role, 
        string? content, 
        List<CustomChatToolCall> toolCalls, 
        Dictionary<string, object?> metadata)
    {
        try
        {
            // Reset tracking variables
            ReflectionSucceeded = false;
            LastError = null;
            
            // Get the OpenAIChatMessageContent type
            var openAiMessageType = typeof(OpenAIChatMessageContent);
            LastError = $"Type found: {openAiMessageType.FullName}";
            
            // Get the FunctionToolCall type
            var functionToolCallType = typeof(OpenAIChatMessageContent).Assembly.GetType("Microsoft.SemanticKernel.Connectors.OpenAI.FunctionToolCall");
            if (functionToolCallType == null)
            {
                LastError = "FunctionToolCall type not found";
                return null;
            }
            LastError = $"FunctionToolCall type found: {functionToolCallType.FullName}";
            
            // Convert our custom tool calls to the internal FunctionToolCall type
            var convertedToolCalls = new List<object>();
            foreach (var toolCall in toolCalls)
            {
                var toolCallConstructor = functionToolCallType.GetConstructor(
                    BindingFlags.NonPublic | BindingFlags.Instance, 
                    null, 
                    new[] { typeof(string), typeof(string), typeof(string) }, 
                    null);
                    
                if (toolCallConstructor == null)
                {
                    LastError = "FunctionToolCall constructor not found";
                    continue;
                }
                
                var internalToolCall = toolCallConstructor.Invoke(new object[] { 
                    toolCall.Id, 
                    toolCall.FunctionName, 
                    toolCall.FunctionArguments 
                });
                
                convertedToolCalls.Add(internalToolCall);
            }
            
            if (convertedToolCalls.Count == 0)
            {
                LastError = "No tool calls could be converted";
                return null;
            }
            
            // Create a new instance of OpenAIChatMessageContent using reflection
            var messageConstructor = openAiMessageType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, 
                null, 
                new[] { typeof(AuthorRole), typeof(string), typeof(IReadOnlyList<>).MakeGenericType(functionToolCallType), typeof(IReadOnlyDictionary<string, object>) }, 
                null);
                
            if (messageConstructor == null)
            {
                // Try to find available constructors
                var availableConstructors = openAiMessageType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
                LastError = $"OpenAIChatMessageContent constructor not found. Available constructors: {availableConstructors.Length}";
                
                foreach (var ctor in availableConstructors)
                {
                    var parameters = ctor.GetParameters();
                    LastError += $"\nConstructor with {parameters.Length} parameters: ({string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))})";
                }
                
                return null;
            }
            
            // Create an array of the internal tool call type
            var toolCallsArray = Array.CreateInstance(functionToolCallType, convertedToolCalls.Count);
            for (int i = 0; i < convertedToolCalls.Count; i++)
            {
                toolCallsArray.SetValue(convertedToolCalls[i], i);
            }
            
            var result = (ChatMessageContent)messageConstructor.Invoke(new object[] { 
                role, 
                content ?? string.Empty, 
                toolCallsArray, 
                metadata 
            });
            
            ReflectionSucceeded = true;
            LastError = "Success";
            return result;
        }
        catch (Exception ex)
        {
            LastError = $"Exception: {ex.Message}\nStack trace: {ex.StackTrace}";
            return null;
        }
    }
} 