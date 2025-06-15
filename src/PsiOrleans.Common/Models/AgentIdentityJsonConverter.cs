using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// JSON converter for AgentIdentity to enable proper serialization.
/// </summary>
public class AgentIdentityJsonConverter : JsonConverter<AgentIdentity>
{
    /// <summary>
    /// Reads and converts the JSON to AgentIdentity.
    /// </summary>
    public override AgentIdentity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return AgentIdentity.Empty;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token for AgentIdentity");
        }

        AgentId id = AgentId.Empty;
        string name = string.Empty;
        AgentRole role = AgentRole.Undecided;
        DateTime createdAt = DateTime.MinValue;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "Id" or "id":
                        id = JsonSerializer.Deserialize<AgentId>(ref reader, options);
                        break;
                    case "Name" or "name":
                        name = reader.GetString() ?? string.Empty;
                        break;
                    case "Role" or "role":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var roleString = reader.GetString();
                            Enum.TryParse<AgentRole>(roleString, true, out role);
                        }
                        else if (reader.TokenType == JsonTokenType.Number)
                        {
                            role = (AgentRole)reader.GetInt32();
                        }
                        break;
                    case "CreatedAt" or "createdAt":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (DateTime.TryParse(reader.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out createdAt))
                            {
                                // Ensure UTC if no timezone info
                                if (createdAt.Kind == DateTimeKind.Unspecified)
                                {
                                    createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
                                }
                            }
                        }
                        break;
                }
            }
        }

        return AgentIdentity.Create(id, name, role, createdAt);
    }

    /// <summary>
    /// Writes AgentIdentity as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, AgentIdentity value, JsonSerializerOptions options)
    {
        if (!value.IsValid)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        
        writer.WritePropertyName("Id");
        JsonSerializer.Serialize(writer, value.Id, options);
        
        writer.WriteString("Name", value.Name);
        writer.WriteString("Role", value.Role.ToString());
        writer.WriteString("CreatedAt", value.CreatedAt.ToString("O")); // ISO 8601 format
        
        writer.WriteEndObject();
    }
} 