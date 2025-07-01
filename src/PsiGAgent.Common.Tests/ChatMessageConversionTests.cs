using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PsiGAgent.Common.Models;
using ChatMessage = PsiGAgent.Common.Models.ChatMessage;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace PsiGAgent.Common.Tests
{
    public class ChatMessageConversionTests
    {
        [Fact]
        public void ConvertChatMessage_WithoutToolCalls_ReturnsExtendedChatMessageContent()
        {
            // Arrange
            var chatMessage = new ChatMessage
            {
                Role = "user",
                Content = "Hello, world!",
                Name = "TestUser",
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object> { { "foo", "bar" } },
                ToolCalls = new List<ToolCall>()
            };

            // Act
            var result = ChatMessageConverter.ToSemanticKernelMessage(chatMessage);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ChatMessageContent>(result);
            Assert.Equal(AuthorRole.User, result.Role);
            Assert.Equal("Hello, world!", result.Content);

#pragma warning disable SKEXP0001
            Assert.Equal("TestUser", result.AuthorName);
#pragma warning restore SKEXP0001

            // Access metadata
            Assert.NotNull(result.Metadata);
            Assert.True(result.Metadata.ContainsKey("foo"));
            Assert.Equal("bar", result.Metadata["foo"]);
        }

        [Fact]
        public void ConvertChatMessage_WithToolCalls_ReturnsCustomChatMessageContent()
        {
            // Arrange
            var toolCalls = new List<ToolCall>
            {
                new ToolCall { FunctionName = "func1", FunctionArguments = "{\"arg\":1}" },
                new ToolCall { FunctionName = "func2", FunctionArguments = "{\"arg\":2}" }
            };
            var chatMessage = new ChatMessage
            {
                Role = "assistant",
                Content = "Here's a tool call.",
                Name = "Bot",
                ToolCalls = toolCalls
            };

            // Act
            var result = ChatMessageConverter.ToSemanticKernelMessage(chatMessage);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ChatMessageContent>(result);
            Assert.Equal(AuthorRole.Assistant, result.Role);
            Assert.Equal("Here's a tool call.", result.Content);

#pragma warning disable SKEXP0001
            Assert.Equal("Bot", result.AuthorName);
#pragma warning restore SKEXP0001

            // Check for function calls in the items collection
            Assert.NotNull(result.Items);
            
            // Count the number of FunctionCallContent items
            var functionCallCount = 0;
            foreach (var item in result.Items)
            {
                if (item is FunctionCallContent)
                {
                    functionCallCount++;
                }
            }
            
            Assert.Equal(2, functionCallCount);
            
            // Verify function call names
            var foundFunc1 = false;
            var foundFunc2 = false;
            
            foreach (var item in result.Items)
            {
                if (item is FunctionCallContent functionCall)
                {
                    if (functionCall.FunctionName == "func1")
                    {
                        foundFunc1 = true;
                    }
                    else if (functionCall.FunctionName == "func2")
                    {
                        foundFunc2 = true;
                    }
                }
            }
            
            Assert.True(foundFunc1);
            Assert.True(foundFunc2);
        }

        [Fact]
        public void ConvertChatMessage_MapsAllFields()
        {
            // Arrange
            var timestamp = DateTime.UtcNow;
            var chatMessage = new ChatMessage
            {
                Role = "system",
                Content = "System message.",
                Name = "System",
                Timestamp = timestamp,
                Metadata = new Dictionary<string, object> { { "meta", 123 } }
            };

            // Act
            var result = ChatMessageConverter.ToSemanticKernelMessage(chatMessage);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ChatMessageContent>(result);
            Assert.Equal(AuthorRole.System, result.Role);
            Assert.Equal("System message.", result.Content);

#pragma warning disable SKEXP0001
            Assert.Equal("System", result.AuthorName);
#pragma warning restore SKEXP0001

            // Access metadata
            Assert.NotNull(result.Metadata);
            Assert.True(result.Metadata.ContainsKey("meta"));
            Assert.Equal(123, result.Metadata["meta"]);
        }

        [Fact]
        public void ConvertChatMessage_ToolCallsMapping()
        {
            // Arrange
            var toolCalls = new List<ToolCall>
            {
                new ToolCall { FunctionName = "search", FunctionArguments = "{\"q\":\"test\"}" }
            };
            var chatMessage = new ChatMessage
            {
                Role = "assistant",
                Content = "Tool call message.",
                ToolCalls = toolCalls
            };

            // Act
            var result = ChatMessageConverter.ToSemanticKernelMessage(chatMessage);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ChatMessageContent>(result);
            
            // Check for function calls in the items collection
            Assert.NotNull(result.Items);
            
            // Find the FunctionCallContent item
            FunctionCallContent? functionCall = null;
            foreach (var item in result.Items)
            {
                if (item is FunctionCallContent fc)
                {
                    functionCall = fc;
                    break;
                }
            }
            
            Assert.NotNull(functionCall);
            
            // We've already verified functionCall is not null with the Assert.NotNull call above
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            Assert.Equal("search", functionCall.FunctionName);
            
            // Check if arguments contain the expected value
            Assert.True(functionCall.Arguments.ContainsKey("q") || functionCall.Arguments.ContainsKey("arguments"));
#pragma warning restore CS8602
        }

        [Fact]
        public void ConvertFromSemanticKernelMessage_WithToolCalls_ConvertsCorrectly()
        {
            // Arrange
            var kernelArgs = new KernelArguments { { "location", "Boston" } };
            var items = new ChatMessageContentItemCollection
            {
                new TextContent("Assistant message with tool call."),
                new FunctionCallContent("get_weather", arguments: kernelArgs)
            };
            var skMessage = new ChatMessageContent(AuthorRole.Assistant, items, metadata: new Dictionary<string, object?> { { "test", "value" } });

#pragma warning disable SKEXP0001
            skMessage.AuthorName = "TestAssistant";
#pragma warning restore SKEXP0001

            // Act
            var result = ChatMessageConverter.FromSemanticKernelMessage(skMessage);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("assistant", result.Role);
            Assert.Equal("TestAssistant", result.Name);
            Assert.Equal("Assistant message with tool call.", result.Content);
            Assert.NotNull(result.Metadata);
            Assert.True(result.Metadata.ContainsKey("test"));
            Assert.Equal("value", result.Metadata["test"]);
            Assert.NotNull(result.ToolCalls);
            Assert.Single(result.ToolCalls);
            Assert.Equal("get_weather", result.ToolCalls[0].FunctionName);

            var resultArgs = JsonSerializer.Deserialize<Dictionary<string, object>>(result.ToolCalls[0].FunctionArguments);
            Assert.NotNull(resultArgs);
            Assert.Equal("Boston", resultArgs["location"].ToString());
        }
    }
}