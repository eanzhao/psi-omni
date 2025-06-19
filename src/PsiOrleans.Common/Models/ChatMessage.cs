using Orleans;

namespace PsiOrleans.Common.Models;

[Serializable]
[GenerateSerializer]
public class ToolCall
{
    [Id(0)] public string FunctionName { get; set; } = string.Empty;
    [Id(1)] public string FunctionArguments { get; set; } = string.Empty;
}

[Serializable]
[GenerateSerializer]
public class ChatMessage
{
    [Id(0)] public string Role { get; set; } = string.Empty;

    [Id(1)] public string Content { get; set; } = string.Empty;

    [Id(2)] public string? Name { get; set; }

    [Id(3)] public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Id(4)] public Dictionary<string, object> Metadata { get; set; } = new();

    [Id(5)] public List<ToolCall> ToolCalls { get; set; } = new();

    /// <summary>
    /// Default constructor
    /// </summary>
    public ChatMessage()
    {
    }

    /// <summary>
    /// Constructor with role and content
    /// </summary>
    public ChatMessage(string role, string? content, string? name = null, List<ToolCall>? toolCalls = null)
    {
        Role = role ?? string.Empty;
        Content = content ?? string.Empty;
        Name = name??string.Empty;
        ToolCalls = toolCalls ?? new List<ToolCall>();
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a system message
    /// </summary>
    public static ChatMessage CreateSystemMessage(string content)
    {
        return new ChatMessage("system", content);
    }

    /// <summary>
    /// Creates a user message
    /// </summary>
    public static ChatMessage CreateUserMessage(string content)
    {
        return new ChatMessage("user", content);
    }

    /// <summary>
    /// Creates an assistant message
    /// </summary>
    public static ChatMessage CreateAssistantMessage(string content)
    {
        return new ChatMessage("assistant", content);
    }
}