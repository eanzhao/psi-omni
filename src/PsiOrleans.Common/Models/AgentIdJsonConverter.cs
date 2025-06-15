using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// JSON converter for AgentId value object to enable proper serialization.
/// </summary>
public class AgentIdJsonConverter : JsonConverter<AgentId>
{
    /// <summary>
    /// Reads and converts the JSON to AgentId.
    /// </summary>
    public override AgentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return AgentId.Empty;
        }

        var value = reader.GetString();
        return AgentId.Parse(value);
    }

    /// <summary>
    /// Writes AgentId as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, AgentId value, JsonSerializerOptions options)
    {
        if (!value.IsValid)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value);
        }
    }
} 