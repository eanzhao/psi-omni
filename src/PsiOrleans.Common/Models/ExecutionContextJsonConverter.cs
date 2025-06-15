using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// JSON converter for ExecutionContext to enable proper serialization.
/// </summary>
public class ExecutionContextJsonConverter : JsonConverter<ExecutionContext>
{
    /// <summary>
    /// Reads and converts the JSON to ExecutionContext.
    /// </summary>
    public override ExecutionContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return ExecutionContext.Empty;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token for ExecutionContext");
        }

        string executionId = string.Empty;
        AgentId agentId = AgentId.Empty;
        string taskDescription = string.Empty;
        ExecutionStatus status = ExecutionStatus.Created;
        DateTime startedAt = DateTime.MinValue;
        DateTime? completedAt = null;
        Dictionary<string, object> metadata = new();
        string? errorMessage = null;

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
                    case "ExecutionId" or "executionId":
                        executionId = reader.GetString() ?? string.Empty;
                        break;
                    case "AgentId" or "agentId":
                        agentId = JsonSerializer.Deserialize<AgentId>(ref reader, options);
                        break;
                    case "TaskDescription" or "taskDescription":
                        taskDescription = reader.GetString() ?? string.Empty;
                        break;
                    case "Status" or "status":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var statusString = reader.GetString();
                            Enum.TryParse<ExecutionStatus>(statusString, true, out status);
                        }
                        else if (reader.TokenType == JsonTokenType.Number)
                        {
                            status = (ExecutionStatus)reader.GetInt32();
                        }
                        break;
                    case "StartedAt" or "startedAt":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (DateTime.TryParse(reader.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out startedAt))
                            {
                                // Ensure UTC if no timezone info
                                if (startedAt.Kind == DateTimeKind.Unspecified)
                                {
                                    startedAt = DateTime.SpecifyKind(startedAt, DateTimeKind.Utc);
                                }
                            }
                        }
                        break;
                    case "CompletedAt" or "completedAt":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (DateTime.TryParse(reader.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var completed))
                            {
                                // Ensure UTC if no timezone info
                                if (completed.Kind == DateTimeKind.Unspecified)
                                {
                                    completed = DateTime.SpecifyKind(completed, DateTimeKind.Utc);
                                }
                                completedAt = completed;
                            }
                        }
                        break;
                    case "Metadata" or "metadata":
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            var metadataDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ref reader, options);
                            if (metadataDict != null)
                            {
                                foreach (var (key, element) in metadataDict)
                                {
                                    metadata[key] = ConvertJsonElement(element);
                                }
                            }
                        }
                        break;
                    case "ErrorMessage" or "errorMessage":
                        errorMessage = reader.GetString();
                        break;
                }
            }
        }

        return ExecutionContext.Create(
            executionId,
            agentId,
            taskDescription,
            status,
            startedAt,
            completedAt,
            metadata,
            errorMessage);
    }

    /// <summary>
    /// Writes ExecutionContext as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, ExecutionContext value, JsonSerializerOptions options)
    {
        if (!value.IsValid)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        
        writer.WriteString("ExecutionId", value.ExecutionId);
        
        writer.WritePropertyName("AgentId");
        JsonSerializer.Serialize(writer, value.AgentId, options);
        
        writer.WriteString("TaskDescription", value.TaskDescription);
        writer.WriteString("Status", value.Status.ToString());
        writer.WriteString("StartedAt", value.StartedAt.ToString("O")); // ISO 8601 format
        
        if (value.CompletedAt.HasValue)
        {
            writer.WriteString("CompletedAt", value.CompletedAt.Value.ToString("O"));
        }
        else
        {
            writer.WriteNull("CompletedAt");
        }
        
        // Write metadata
        writer.WritePropertyName("Metadata");
        writer.WriteStartObject();
        foreach (var (key, metadataValue) in value.Metadata)
        {
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, metadataValue, options);
        }
        writer.WriteEndObject();
        
        if (value.ErrorMessage != null)
        {
            writer.WriteString("ErrorMessage", value.ErrorMessage);
        }
        else
        {
            writer.WriteNull("ErrorMessage");
        }
        
        writer.WriteEndObject();
    }

    /// <summary>
    /// Converts a JsonElement to an appropriate object type.
    /// </summary>
    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.ToString()
        };
    }
} 