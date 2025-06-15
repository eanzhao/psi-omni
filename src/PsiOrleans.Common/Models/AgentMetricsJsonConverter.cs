using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// JSON converter for AgentMetrics to enable proper serialization.
/// </summary>
public class AgentMetricsJsonConverter : JsonConverter<AgentMetrics>
{
    /// <summary>
    /// Reads and converts the JSON to AgentMetrics.
    /// </summary>
    public override AgentMetrics Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return AgentMetrics.Empty;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token for AgentMetrics");
        }

        int totalTasks = 0;
        int successfulTasks = 0;
        int failedTasks = 0;
        TimeSpan totalExecutionTime = TimeSpan.Zero;

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
                    case "TotalTasks" or "totalTasks":
                        totalTasks = reader.GetInt32();
                        break;
                    case "SuccessfulTasks" or "successfulTasks":
                        successfulTasks = reader.GetInt32();
                        break;
                    case "FailedTasks" or "failedTasks":
                        failedTasks = reader.GetInt32();
                        break;
                    case "TotalExecutionTime" or "totalExecutionTime":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var timeSpanString = reader.GetString();
                            if (TimeSpan.TryParse(timeSpanString, out var parsedTimeSpan))
                            {
                                totalExecutionTime = parsedTimeSpan;
                            }
                        }
                        break;
                }
            }
        }

        return new AgentMetrics(totalTasks, successfulTasks, failedTasks, totalExecutionTime);
    }

    /// <summary>
    /// Writes AgentMetrics as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, AgentMetrics value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        writer.WriteNumber("TotalTasks", value.TotalTasks);
        writer.WriteNumber("SuccessfulTasks", value.SuccessfulTasks);
        writer.WriteNumber("FailedTasks", value.FailedTasks);
        writer.WriteString("TotalExecutionTime", value.TotalExecutionTime.ToString());
        
        writer.WriteEndObject();
    }
} 