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

The converter attempts to create `OpenAIChatMessageContent` instances when tool calls are present using the following approach:

1. First, it tries to use reflection to create `ChatToolCall` instances from the OpenAI SDK:
   - Gets the internal function type (`InternalChatCompletionMessageToolCallFunction`)
   - Creates instances of this internal type
   - Creates `ChatToolCall` instances using the internal constructor
   - Creates an `OpenAIChatMessageContent` with these tool calls

2. If the reflection approach fails (which may happen if the internal structure of the Semantic Kernel or OpenAI SDK changes), it falls back to using the public constructor of `OpenAIChatMessageContent` that takes a `ChatMessageContentItemCollection`.

3. If all approaches fail, it creates a regular `ChatMessageContent` with function calls in the items collection.

This implementation is designed to be robust against changes in the internal structure of the libraries it depends on, while still providing the best possible conversion between message formats.

### Dependencies

- Microsoft.SemanticKernel.Abstractions
- Microsoft.SemanticKernel.Connectors.OpenAI
- OpenAI

### Note

The reflection-based approach may break if the internal structure of the Semantic Kernel or OpenAI SDK changes significantly. If this happens, the converter will automatically fall back to the public API approach.

### Usage

```csharp
// Convert from PsiOrleans.Common.Models.ChatMessage to Semantic Kernel ChatMessageContent
var message = new ChatMessage("user", "Hello, world!");
var skMessage = ChatMessageConverter.ToSemanticKernelMessage(message);

// Use the converted message with Semantic Kernel
var kernel = Kernel.CreateBuilder().Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var result = await chatCompletionService.GetChatMessageContentsAsync(new[] { skMessage });
```

## Models

The library includes several model classes used throughout the PsiOrleans system:

- `ChatMessage`: Represents a chat message with role, content, and optional tool calls
- `ToolCall`: Represents a function call in a chat message
- `AgentConfiguration`: Configuration settings for an agent
- `AgentRole`: Defines the role of an agent in the system
- `AgentState`: Represents the current state of an agent
- `TaskAnalysisResult`: Contains the result of analyzing a task 