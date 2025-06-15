using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// Immutable agent identity that combines a unique identifier with rich metadata.
/// Provides the foundation for agent context and identification throughout the system.
/// </summary>
[JsonConverter(typeof(AgentIdentityJsonConverter))]
public class AgentIdentity : IEquatable<AgentIdentity>
{
    /// <summary>
    /// Gets the unique identifier for this agent.
    /// </summary>
    public AgentId Id { get; init; }

    /// <summary>
    /// Gets the human-readable name of this agent.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the role/type of this agent in the system.
    /// </summary>
    public AgentRole Role { get; init; }

    /// <summary>
    /// Gets the date and time when this agent identity was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets whether this agent identity is valid (has valid ID and name).
    /// </summary>
    public bool IsValid => Id.IsValid && !string.IsNullOrWhiteSpace(Name);

    /// <summary>
    /// Private constructor for creating agent identities.
    /// </summary>
    private AgentIdentity() { }

    /// <summary>
    /// Creates a new agent identity with the specified parameters.
    /// </summary>
    /// <param name="id">The unique agent identifier.</param>
    /// <param name="name">The human-readable name of the agent.</param>
    /// <param name="role">The role/type of the agent.</param>
    /// <param name="createdAt">The creation timestamp (defaults to current UTC time).</param>
    /// <returns>A new AgentIdentity instance.</returns>
    public static AgentIdentity Create(AgentId id, string name, AgentRole role, DateTime? createdAt = null)
    {
        return new AgentIdentity
        {
            Id = id,
            Name = name ?? string.Empty,
            Role = role,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new agent identity with a newly generated unique identifier.
    /// </summary>
    /// <param name="name">The human-readable name of the agent.</param>
    /// <param name="role">The role/type of the agent.</param>
    /// <returns>A new AgentIdentity instance with a unique generated ID.</returns>
    public static AgentIdentity CreateNew(string name, AgentRole role)
    {
        return Create(AgentId.NewId(), name, role);
    }

    /// <summary>
    /// Creates a new agent identity with an updated name.
    /// </summary>
    /// <param name="name">The new name for the agent.</param>
    /// <returns>A new AgentIdentity instance with the updated name.</returns>
    public AgentIdentity WithName(string name)
    {
        return Create(Id, name, Role, CreatedAt);
    }

    /// <summary>
    /// Creates a new agent identity with an updated role.
    /// </summary>
    /// <param name="role">The new role for the agent.</param>
    /// <returns>A new AgentIdentity instance with the updated role.</returns>
    public AgentIdentity WithRole(AgentRole role)
    {
        return Create(Id, Name, role, CreatedAt);
    }

    /// <summary>
    /// Gets an empty/invalid agent identity.
    /// </summary>
    public static AgentIdentity Empty => new()
    {
        Id = AgentId.Empty,
        Name = string.Empty,
        Role = AgentRole.Undecided,
        CreatedAt = DateTime.MinValue
    };

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(AgentIdentity? left, AgentIdentity? right) => 
        ReferenceEquals(left, right) || (left?.Equals(right) == true);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(AgentIdentity? left, AgentIdentity? right) => !(left == right);

    /// <summary>
    /// Checks equality with another AgentIdentity.
    /// </summary>
    /// <param name="other">The other AgentIdentity to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public bool Equals(AgentIdentity? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return Id.Equals(other.Id) &&
               string.Equals(Name, other.Name, StringComparison.Ordinal) &&
               Role == other.Role &&
               CreatedAt.Equals(other.CreatedAt);
    }

    /// <summary>
    /// Checks equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if equal, false otherwise.</returns>
    public override bool Equals(object? obj)
    {
        return Equals(obj as AgentIdentity);
    }

    /// <summary>
    /// Gets the hash code for this AgentIdentity.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Role, CreatedAt);
    }

    /// <summary>
    /// Returns a string representation of this agent identity.
    /// </summary>
    /// <returns>A formatted string containing the agent's key information.</returns>
    public override string ToString()
    {
        return $"AgentIdentity(Id: {Id}, Name: '{Name}', Role: {Role}, Created: {CreatedAt:yyyy-MM-dd HH:mm:ss} UTC)";
    }
} 