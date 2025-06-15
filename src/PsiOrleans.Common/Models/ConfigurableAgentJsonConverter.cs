using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// JSON converter for ConfigurableAgent to enable proper serialization.
/// </summary>
public class ConfigurableAgentJsonConverter : JsonConverter<ConfigurableAgent>
{
    /// <summary>
    /// Reads and converts the JSON to ConfigurableAgent.
    /// </summary>
    public override ConfigurableAgent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return ConfigurableAgent.Empty;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token for ConfigurableAgent");
        }

        AgentIdentity identity = AgentIdentity.Empty;
        AgentConfiguration configuration = new();
        ExecutionContext execution = ExecutionContext.Empty;
        List<AgentStep> workflowSteps = new();
        AgentStep? currentStep = null;
        AgentMetrics metrics = AgentMetrics.Empty;
        Dictionary<string, object> workingMemory = new();
        DateTime createdAt = DateTime.MinValue;
        DateTime lastUpdated = DateTime.MinValue;

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
                    case "Identity" or "identity":
                        var identityResult = JsonSerializer.Deserialize<AgentIdentity>(ref reader, options);
                        identity = identityResult ?? AgentIdentity.Empty;
                        break;
                    case "Configuration" or "configuration":
                        var configResult = JsonSerializer.Deserialize<AgentConfiguration>(ref reader, options);
                        configuration = configResult ?? new AgentConfiguration();
                        break;
                    case "Execution" or "execution":
                        var executionResult = JsonSerializer.Deserialize<ExecutionContext>(ref reader, options);
                        execution = executionResult ?? ExecutionContext.Empty;
                        break;
                    case "WorkflowSteps" or "workflowSteps":
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            var stepsResult = JsonSerializer.Deserialize<List<AgentStep>>(ref reader, options);
                            workflowSteps = stepsResult ?? new List<AgentStep>();
                        }
                        break;
                    case "CurrentStep" or "currentStep":
                        if (reader.TokenType != JsonTokenType.Null)
                        {
                            currentStep = JsonSerializer.Deserialize<AgentStep>(ref reader, options);
                        }
                        break;
                    case "Metrics" or "metrics":
                        var metricsResult = JsonSerializer.Deserialize<AgentMetrics>(ref reader, options);
                        metrics = metricsResult ?? AgentMetrics.Empty;
                        break;
                    case "WorkingMemory" or "workingMemory":
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            var memoryDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ref reader, options);
                            if (memoryDict != null)
                            {
                                foreach (var (key, element) in memoryDict)
                                {
                                    workingMemory[key] = ConvertJsonElement(element);
                                }
                            }
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
                    case "LastUpdated" or "lastUpdated":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            if (DateTime.TryParse(reader.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out lastUpdated))
                            {
                                // Ensure UTC if no timezone info
                                if (lastUpdated.Kind == DateTimeKind.Unspecified)
                                {
                                    lastUpdated = DateTime.SpecifyKind(lastUpdated, DateTimeKind.Utc);
                                }
                            }
                        }
                        break;
                }
            }
        }

        return ConfigurableAgent.Create(
            identity,
            configuration,
            execution,
            workflowSteps,
            currentStep,
            metrics,
            workingMemory,
            createdAt,
            lastUpdated);
    }

    /// <summary>
    /// Writes ConfigurableAgent as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, ConfigurableAgent value, JsonSerializerOptions options)
    {

        writer.WriteStartObject();
        
        writer.WritePropertyName("Identity");
        JsonSerializer.Serialize(writer, value.Identity, options);
        
        writer.WritePropertyName("Configuration");
        JsonSerializer.Serialize(writer, value.Configuration, options);
        
        writer.WritePropertyName("Execution");
        JsonSerializer.Serialize(writer, value.Execution, options);
        
        writer.WritePropertyName("WorkflowSteps");
        JsonSerializer.Serialize(writer, value.WorkflowSteps, options);
        
        writer.WritePropertyName("CurrentStep");
        if (value.CurrentStep != null)
        {
            JsonSerializer.Serialize(writer, value.CurrentStep, options);
        }
        else
        {
            writer.WriteNullValue();
        }
        
        writer.WritePropertyName("Metrics");
        JsonSerializer.Serialize(writer, value.Metrics, options);
        
        // Write working memory
        writer.WritePropertyName("WorkingMemory");
        writer.WriteStartObject();
        foreach (var (key, memoryValue) in value.WorkingMemory)
        {
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, memoryValue, options);
        }
        writer.WriteEndObject();
        
        writer.WriteString("CreatedAt", value.CreatedAt.ToString("O")); // ISO 8601 format
        writer.WriteString("LastUpdated", value.LastUpdated.ToString("O")); // ISO 8601 format
        
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