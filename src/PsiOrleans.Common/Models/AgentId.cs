using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Strong-typed, immutable identifier for agents throughout the system.
/// Replaces string-based identifiers with validated, testable value object.
/// </summary>
[JsonConverter(typeof(AgentIdJsonConverter))]
public readonly struct AgentId : IEquatable<AgentId>
{
    private readonly string _value;

    /// <summary>
    /// The underlying string value of the agent identifier.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Indicates whether this AgentId has a valid, non-empty value.
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(_value);

    private AgentId(string value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new unique agent identifier.
    /// </summary>
    /// <returns>A new AgentId with a unique value.</returns>
    public static AgentId NewId()
    {
        return new AgentId($"agent-{Guid.NewGuid():N}");
    }

    /// <summary>
    /// Creates an AgentId from a string value.
    /// Invalid strings result in an invalid AgentId.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <returns>An AgentId representing the value.</returns>
    public static AgentId Parse(string? value)
    {
        return new AgentId(value ?? string.Empty);
    }

    /// <summary>
    /// Attempts to create an AgentId from a string value.
    /// </summary>
    /// <param name="value">The string value to parse.</param>
    /// <param name="agentId">The resulting AgentId if successful.</param>
    /// <returns>True if the value is valid, false otherwise.</returns>
    public static bool TryParse(string? value, out AgentId agentId)
    {
        agentId = new AgentId(value ?? string.Empty);
        return agentId.IsValid;
    }

    /// <summary>
    /// Gets an empty/invalid AgentId.
    /// </summary>
    public static AgentId Empty => new(string.Empty);

    /// <summary>
    /// Implicit conversion from string to AgentId.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>An AgentId representing the string.</returns>
    public static implicit operator AgentId(string value) => Parse(value);

    /// <summary>
    /// Implicit conversion from AgentId to string.
    /// </summary>
    /// <param name="agentId">The AgentId to convert.</param>
    /// <returns>The string value of the AgentId.</returns>
    public static implicit operator string(AgentId agentId) => agentId.Value;

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(AgentId left, AgentId right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(AgentId left, AgentId right) => !left.Equals(right);

    /// <summary>
    /// Checks equality with another AgentId.
    /// </summary>
    /// <param name="other">The other AgentId to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public bool Equals(AgentId other)
    {
        return string.Equals(Value, other.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        return obj is AgentId other && Equals(other);
    }

    /// <summary>
    /// Gets the hash code for this AgentId.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return Value.GetHashCode(StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns the string representation of this AgentId.
    /// </summary>
    /// <returns>The underlying string value.</returns>
    public override string ToString()
    {
        return Value;
    }
} 