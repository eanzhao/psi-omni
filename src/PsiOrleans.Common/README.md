# PsiOrleans.Common

This library contains common models and interfaces for the PsiOrleans project.

## ChatMessageConverter

The `ChatMessageConverter` class provides functionality to convert bidirectionally between `PsiOrleans.Common.Models.ChatMessage` and the Semantic Kernel's `Microsoft.SemanticKernel.ChatCompletion.ChatMessageContent`.

### Features

-   **Bidirectional Conversion**: Converts to and from the Semantic Kernel's message format.
-   **Role Mapping**: Translates between string roles and the `AuthorRole` enum.
-   **Metadata Preservation**: Keeps metadata intact during conversion.
-   **Tool Call Handling**: Correctly maps tool calls in both directions.
-   **Author Name Support**: Preserves the author's name across conversions.

### `ToSemanticKernelMessage` (Our Model → SK Model)

The conversion to the Semantic Kernel model is designed for simplicity and stability. It exclusively uses the base `ChatMessageContent` class, which is fully capable of handling tool calls via `FunctionCallContent` items in its `Items` collection. This approach avoids reflection and reliance on internal or inaccessible constructors, ensuring the converter remains robust across library updates.

### `FromSemanticKernelMessage` (SK Model → Our Model)

The conversion from a Semantic Kernel message back to our serializable `ChatMessage` is designed for resilience. The logic is as follows:

1.  It first checks if the message is of the specific `OpenAIChatMessageContent` type. If so, it uses reflection to access a potential internal `ToolCalls` property, which may be used in some versions of the library.
2.  If the reflection check fails or the message is a standard `ChatMessageContent`, it falls back to reading the public `Items` collection to find any `FunctionCallContent` objects.

This hybrid approach ensures that tool calls are captured correctly, regardless of the specific runtime type of the message object.

### Dependencies

-   Microsoft.SemanticKernel

### Usage

```csharp
// --- To Semantic Kernel Message ---

var psiMessage = new ChatMessage
{
    Role = "assistant",
    Content = "I can help with that.",
    ToolCalls = new List<ToolCall> { new ToolCall { FunctionName = "get_weather" } }
};

// Convert to the Semantic Kernel's message format.
var skMessage = ChatMessageConverter.ToSemanticKernelMessage(psiMessage);


// --- From Semantic Kernel Message ---

// Create a Semantic Kernel message (e.g., received from an agent).
var receivedSkMessage = new ChatMessageContent(
    AuthorRole.Assistant,
    new ChatMessageContentItemCollection
    {
        new TextContent("Searching for weather..."),
        new FunctionCallContent("get_weather", arguments: new KernelArguments { { "location", "New York" } })
    });

// Convert back to our serializable ChatMessage.
var convertedPsiMessage = ChatMessageConverter.FromSemanticKernelMessage(receivedSkMessage);
```

## Models

The library includes several model classes used throughout the PsiOrleans system:

-   `ChatMessage`: Represents a chat message with role, content, and optional tool calls
-   `ToolCall`: Represents a function call in a chat message
-   `AgentConfiguration`: Configuration settings for an agent
-   `AgentRole`: Defines the role of an agent in the system
-   `AgentState`: Represents the current state of an agent
-   `TaskAnalysisResult`: Contains the result of analyzing a task 