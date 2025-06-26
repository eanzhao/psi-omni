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

The converter attempts to create `OpenAIChatMessageContent` instances when tool calls are present, but falls back to regular `ChatMessageContent` if the reflection-based approach fails. This is necessary because `OpenAIChatMessageContent` has internal constructors that cannot be directly accessed.

The current implementation has the following limitations:

1. **Reflection Dependency**: The code uses reflection to create instances of internal types like `FunctionToolCall` and `OpenAIChatMessageContent`. This approach may break if the internal structure of Semantic Kernel changes.

2. **Limited Tool Call Support**: Only function-type tool calls are supported. Other types of tool calls (like retrieval) are not currently implemented.

3. **Semantic Kernel Version Dependency**: The code is designed to work with Semantic Kernel 1.57.0. Changes in future versions may require updates to the converter.

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