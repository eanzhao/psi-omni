using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using PsiOrleans.Common.Interfaces;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Unified agent state that manages conversation history, working memory, and configuration
/// in a framework-agnostic manner. This is the core state object for PsiOrleans agents.
/// </summary>
public class UnifiedAgentState : IAgentState
{
    private readonly List<ChatMessage> _chatMessages;
    private readonly Dictionary<string, object> _workingMemory;

    /// <summary>
    /// Unique identifier for the agent
    /// </summary>
    public string AgentId { get; }

    /// <summary>
    /// Agent configuration including model settings, prompts, and behavior parameters
    /// </summary>
    public AgentConfiguration Configuration { get; private set; }

    /// <summary>
    /// Read-only collection of chat messages in chronological order
    /// </summary>
    public IReadOnlyList<ChatMessage> ChatMessages => new ReadOnlyCollection<ChatMessage>(_chatMessages);

    /// <summary>
    /// Read-only dictionary of working memory key-value pairs
    /// </summary>
    public IReadOnlyDictionary<string, object> WorkingMemory => new ReadOnlyDictionary<string, object>(_workingMemory);

    /// <summary>
    /// Timestamp when the agent state was created
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Timestamp when the agent state was last modified
    /// </summary>
    public DateTime LastModified { get; private set; }

    /// <summary>
    /// Creates a new unified agent state with the specified agent ID and configuration
    /// </summary>
    /// <param name="agentId">Unique identifier for the agent</param>
    /// <param name="configuration">Initial agent configuration</param>
    /// <exception cref="ArgumentException">Thrown when agentId is null, empty, or whitespace</exception>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
    public UnifiedAgentState(string agentId, AgentConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("Agent ID cannot be null, empty, or whitespace.", nameof(agentId));

        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        AgentId = agentId;
        _chatMessages = new List<ChatMessage>();
        _workingMemory = new Dictionary<string, object>();
        
        var now = DateTime.UtcNow;
        CreatedAt = now;
        LastModified = now;
    }

    /// <summary>
    /// JSON constructor for deserialization
    /// </summary>
    [JsonConstructor]
    public UnifiedAgentState(
        string agentId, 
        AgentConfiguration configuration, 
        List<ChatMessage> chatMessagesForSerialization, 
        Dictionary<string, object> workingMemoryForSerialization,
        DateTime createdAt,
        DateTime lastModified)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            throw new ArgumentException("Agent ID cannot be null, empty, or whitespace.", nameof(agentId));

        AgentId = agentId;
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _chatMessages = chatMessagesForSerialization ?? new List<ChatMessage>();
        _workingMemory = workingMemoryForSerialization ?? new Dictionary<string, object>();
        CreatedAt = createdAt;
        LastModified = lastModified;
    }

    /// <summary>
    /// Adds a chat message with the specified role and content
    /// </summary>
    /// <param name="role">Message role (system, user, assistant)</param>
    /// <param name="content">Message content</param>
    public void AddChatMessage(string role, string content)
    {
        var message = new ChatMessage(role, content);
        AddChatMessage(message);
    }

    /// <summary>
    /// Adds a chat message object to the conversation history
    /// </summary>
    /// <param name="message">Chat message to add</param>
    /// <exception cref="ArgumentNullException">Thrown when message is null</exception>
    public void AddChatMessage(ChatMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        _chatMessages.Add(message);
        UpdateLastModified();
    }

    /// <summary>
    /// Sets a value in working memory with the specified key
    /// </summary>
    /// <param name="key">Memory key</param>
    /// <param name="value">Value to store</param>
    /// <exception cref="ArgumentException">Thrown when key is null, empty, or whitespace</exception>
    public void SetWorkingMemoryValue(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be null, empty, or whitespace.", nameof(key));

        _workingMemory[key] = value;
        UpdateLastModified();
    }

    /// <summary>
    /// Gets a typed value from working memory
    /// </summary>
    /// <typeparam name="T">Expected type of the value</typeparam>
    /// <param name="key">Memory key</param>
    /// <returns>Value cast to the specified type, or default if key doesn't exist</returns>
    /// <exception cref="InvalidCastException">Thrown when value cannot be cast to the specified type</exception>
    public T? GetWorkingMemoryValue<T>(string key)
    {
        if (!_workingMemory.ContainsKey(key))
            return default(T);

        return (T)_workingMemory[key];
    }

    /// <summary>
    /// Attempts to get a typed value from working memory
    /// </summary>
    /// <typeparam name="T">Expected type of the value</typeparam>
    /// <param name="key">Memory key</param>
    /// <param name="value">Output value if successful</param>
    /// <returns>True if value was retrieved and cast successfully, false otherwise</returns>
    public bool TryGetWorkingMemoryValue<T>(string key, out T? value)
    {
        try
        {
            if (_workingMemory.ContainsKey(key))
            {
                value = (T)_workingMemory[key];
                return true;
            }
        }
        catch (InvalidCastException)
        {
            // Cast failed, fall through to return false
        }

        value = default(T);
        return false;
    }

    /// <summary>
    /// Removes a value from working memory
    /// </summary>
    /// <param name="key">Memory key to remove</param>
    /// <returns>True if the key was found and removed, false otherwise</returns>
    public bool RemoveWorkingMemoryValue(string key)
    {
        var removed = _workingMemory.Remove(key);
        if (removed)
        {
            UpdateLastModified();
        }
        return removed;
    }

    /// <summary>
    /// Clears all working memory entries
    /// </summary>
    public void ClearWorkingMemory()
    {
        _workingMemory.Clear();
        UpdateLastModified();
    }

    /// <summary>
    /// Clears all chat messages from the conversation history
    /// </summary>
    public void ClearChatMessages()
    {
        _chatMessages.Clear();
        UpdateLastModified();
    }

    /// <summary>
    /// Gets chat messages that occurred after the specified timestamp
    /// </summary>
    /// <param name="since">Timestamp to filter from</param>
    /// <returns>List of chat messages after the specified time</returns>
    public List<ChatMessage> GetChatMessagesSince(DateTime since)
    {
        return _chatMessages.Where(m => m.Timestamp > since).ToList();
    }

    /// <summary>
    /// Gets chat messages with the specified role
    /// </summary>
    /// <param name="role">Role to filter by (system, user, assistant)</param>
    /// <returns>List of chat messages with the specified role</returns>
    public List<ChatMessage> GetChatMessagesByRole(string role)
    {
        return _chatMessages.Where(m => m.Role == role).ToList();
    }

    /// <summary>
    /// Updates the agent configuration
    /// </summary>
    /// <param name="configuration">New configuration</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
    public void UpdateConfiguration(AgentConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        UpdateLastModified();
    }

    /// <summary>
    /// Creates a deep copy snapshot of the current state
    /// </summary>
    /// <returns>Independent copy of the agent state</returns>
    public UnifiedAgentState GetStateSnapshot()
    {
        // Create deep copies of mutable collections
        var chatMessagesCopy = new List<ChatMessage>(_chatMessages);
        var workingMemoryCopy = new Dictionary<string, object>(_workingMemory);

        return new UnifiedAgentState(
            AgentId,
            Configuration, // AgentConfiguration is immutable
            chatMessagesCopy,
            workingMemoryCopy,
            CreatedAt,
            LastModified);
    }

    /// <summary>
    /// Returns a string representation of the agent state
    /// </summary>
    /// <returns>Formatted string with key state information</returns>
    public override string ToString()
    {
        return $"UnifiedAgentState[AgentId={AgentId}, Messages={_chatMessages.Count}, WorkingMemory={_workingMemory.Count}, LastModified={LastModified:yyyy-MM-dd HH:mm:ss}]";
    }

    /// <summary>
    /// Updates the LastModified timestamp to the current UTC time
    /// </summary>
    private void UpdateLastModified()
    {
        LastModified = DateTime.UtcNow;
    }

    /// <summary>
    /// JSON serialization properties for internal collections
    /// </summary>
    [JsonPropertyName("chatMessages")]
    public List<ChatMessage> ChatMessagesForSerialization => _chatMessages;

    /// <summary>
    /// JSON serialization properties for internal collections
    /// </summary>
    [JsonPropertyName("workingMemory")]
    public Dictionary<string, object> WorkingMemoryForSerialization => _workingMemory;
} 