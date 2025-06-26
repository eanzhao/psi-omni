using OpenAI;
using OpenAI.Chat;
using System;
using System.Reflection;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        try
        {
            // Try to get the internal type
            var internalType = typeof(ChatToolCall).Assembly.GetType("OpenAI.Chat.InternalChatCompletionMessageToolCallFunction");
            if (internalType == null)
            {
                Console.WriteLine("Could not find InternalChatCompletionMessageToolCallFunction type");
                return;
            }
            
            // Create an instance of the internal type using reflection
            var constructor = internalType.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(BinaryData) },
                null
            );
            
            if (constructor == null)
            {
                Console.WriteLine("Could not find constructor for InternalChatCompletionMessageToolCallFunction");
                return;
            }
            
            var internalFunction = constructor.Invoke(new object[] { 
                "test_function", 
                BinaryData.FromString("{\"arg1\": \"value1\"}") 
            });
            
            if (internalFunction == null)
            {
                Console.WriteLine("Could not create InternalChatCompletionMessageToolCallFunction instance");
                return;
            }
            
            // Create a ChatToolCall
            var toolCallConstructor = typeof(ChatToolCall).GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(string), internalType, typeof(ChatToolCallKind), typeof(IDictionary<string, BinaryData>) },
                null
            );
            
            if (toolCallConstructor == null)
            {
                Console.WriteLine("Could not find constructor for ChatToolCall");
                return;
            }
            
            var toolCall = toolCallConstructor.Invoke(new object[] { 
                "call_123", 
                internalFunction, 
                ChatToolCallKind.Function, 
                new Dictionary<string, BinaryData>() 
            }) as ChatToolCall;
            
            if (toolCall == null)
            {
                Console.WriteLine("Could not create ChatToolCall instance");
                return;
            }
            
            Console.WriteLine($"Successfully created ChatToolCall: {toolCall.Id}, {toolCall.FunctionName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating ChatToolCall: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
