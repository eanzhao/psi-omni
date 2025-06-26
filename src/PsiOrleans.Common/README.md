# ChatMessageConverter

The `ChatMessageConverter` class provides functionality to convert between `PsiOrleans.Common.Models.ChatMessage` and Semantic Kernel message types.

## Overview

This implementation converts `ChatMessage` objects to Semantic Kernel's `ChatMessageContent` type, handling all the necessary conversions for role, content, metadata, and tool calls.

## Implementation Details

The converter uses the official Semantic Kernel API to create ChatMessageContent instances:

1. Converts the role string to the appropriate `AuthorRole` enum value
2. Creates a `ChatMessageContent` instance with the proper content and metadata
3. Handles tool calls by creating appropriate `FunctionCallContent` items in the message's item collection
4. Sets the author name if provided

## Usage

```csharp
// Convert a ChatMessage to a Semantic Kernel message
var chatMessage = new ChatMessage
{
    Role = "user",
    Content = "Hello, world!",
    Name = "TestUser",
    Metadata = new Dictionary<string, object> { { "foo", "bar" } }
};

var skMessage = ChatMessageConverter.ToSemanticKernelMessage(chatMessage);

// The result will be a ChatMessageContent instance
// with all properties properly mapped
```

## Features

- Converts between role string and `AuthorRole` enum
- Maps content and name properties
- Preserves metadata in the message's metadata dictionary
- Handles tool calls conversion for function arguments
- Parses function arguments as JSON when possible 