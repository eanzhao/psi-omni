# PsiOrleans.Common

This library contains common models and interfaces for the PsiOrleans project.

## ChatMessageConverter

The `ChatMessageConverter` class provides functionality to convert between `PsiOrleans.Common.Models.ChatMessage` and Semantic Kernel message types (`Microsoft.SemanticKernel.ChatMessageContent`).

### Features

- Converts role strings to `AuthorRole` enum values
- Preserves metadata between message formats
- Handles tool calls conversion
- Supports author names

### Implementation Details

The converter creates `OpenAIChatMessageContent` instances for messages with tool calls using the following approach:

1. It creates a `ChatMessageContentItemCollection` with:
   - Text content (if available)
   - Function call content items for each tool call

2. It then uses the public constructor of `OpenAIChatMessageContent` that takes a `ChatMessageContentItemCollection`.

3. If that fails, it falls back to creating a regular `ChatMessageContent` with function calls in the items collection.

This implementation is simple, robust, and less likely to break with SDK updates since it uses only public APIs.

### Dependencies

- Microsoft.SemanticKernel.Abstractions
- Microsoft.SemanticKernel.Connectors.OpenAI

### Usage

```csharp
// Create a ChatMessage with tool calls
var chatMessage = new ChatMessage
{
    Role = "assistant",
    Content = "I'll help you with that.",
    ToolCalls = new List<ToolCall>
    {
        new ToolCall
        {
            FunctionName = "get_weather",
            FunctionArguments = "{\"location\": \"New York\", \"unit\": \"celsius\"}"
        }
    }
};

// Convert to Semantic Kernel message
var skMessage = ChatMessageConverter.ToSemanticKernelMessage(chatMessage);

// The result will be an OpenAIChatMessageContent with tool calls
// or a regular ChatMessageContent if conversion fails
```

## Models

The library includes several model classes used throughout the PsiOrleans system:

- `ChatMessage`: Represents a chat message with role, content, and optional tool calls
- `ToolCall`: Represents a function call in a chat message
- `AgentConfiguration`: Configuration settings for an agent
- `AgentRole`: Defines the role of an agent in the system
- `AgentState`: Represents the current state of an agent
- `TaskAnalysisResult`: Contains the result of analyzing a task 