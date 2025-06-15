using System.Text.Json;
using System.Text.Json.Serialization;

namespace PsiOrleans.Common.Models;

/// <summary>
/// JSON converter for AgentStep to enable proper serialization.
/// </summary>
public class AgentStepJsonConverter : JsonConverter<AgentStep>
{
    /// <summary>
    /// Reads and converts the JSON to AgentStep.
    /// </summary>
    public override AgentStep Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return AgentStep.Empty;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token for AgentStep");
        }

        string stepId = string.Empty;
        StepType stepType = StepType.Task;
        AgentIdentity agent = AgentIdentity.Empty;
        ExecutionContext execution = ExecutionContext.Empty;
        string instruction = string.Empty;
        Dictionary<string, object> inputs = new();
        Dictionary<string, object> outputs = new();
        List<string> dependencies = new();
        int priority = 0;
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
                    case "StepId" or "stepId":
                        stepId = reader.GetString() ?? string.Empty;
                        break;
                    case "StepType" or "stepType":
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var stepTypeString = reader.GetString();
                            if (!string.IsNullOrEmpty(stepTypeString))
                            {
                                Enum.TryParse<StepType>(stepTypeString, true, out stepType);
                            }
                        }
                        else if (reader.TokenType == JsonTokenType.Number)
                        {
                            stepType = (StepType)reader.GetInt32();
                        }
                        break;
                    case "Agent" or "agent":
                        var agentResult = JsonSerializer.Deserialize<AgentIdentity>(ref reader, options);
                        agent = agentResult ?? AgentIdentity.Empty;
                        break;
                    case "Execution" or "execution":
                        var executionResult = JsonSerializer.Deserialize<ExecutionContext>(ref reader, options);
                        execution = executionResult ?? ExecutionContext.Empty;
                        break;
                    case "Instruction" or "instruction":
                        instruction = reader.GetString() ?? string.Empty;
                        break;
                    case "Inputs" or "inputs":
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            var inputsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ref reader, options);
                            if (inputsDict != null)
                            {
                                foreach (var (key, element) in inputsDict)
                                {
                                    inputs[key] = ConvertJsonElement(element);
                                }
                            }
                        }
                        break;
                    case "Outputs" or "outputs":
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            var outputsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ref reader, options);
                            if (outputsDict != null)
                            {
                                foreach (var (key, element) in outputsDict)
                                {
                                    outputs[key] = ConvertJsonElement(element);
                                }
                            }
                        }
                        break;
                    case "Dependencies" or "dependencies":
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            var dependenciesArray = JsonSerializer.Deserialize<string[]>(ref reader, options);
                            if (dependenciesArray != null)
                            {
                                dependencies.AddRange(dependenciesArray);
                            }
                        }
                        break;
                    case "Priority" or "priority":
                        if (reader.TokenType == JsonTokenType.Number)
                        {
                            priority = reader.GetInt32();
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

        return AgentStep.Create(
            stepId,
            stepType,
            agent,
            instruction,
            inputs,
            outputs,
            dependencies,
            priority,
            execution,
            createdAt);
    }

    /// <summary>
    /// Writes AgentStep as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, AgentStep value, JsonSerializerOptions options)
    {
        if (!value.IsValid)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        
        writer.WriteString("StepId", value.StepId);
        writer.WriteString("StepType", value.StepType.ToString());
        
        writer.WritePropertyName("Agent");
        JsonSerializer.Serialize(writer, value.Agent, options);
        
        writer.WritePropertyName("Execution");
        JsonSerializer.Serialize(writer, value.Execution, options);
        
        writer.WriteString("Instruction", value.Instruction);
        
        // Write inputs
        writer.WritePropertyName("Inputs");
        writer.WriteStartObject();
        foreach (var (key, inputValue) in value.Inputs)
        {
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, inputValue, options);
        }
        writer.WriteEndObject();
        
        // Write outputs
        writer.WritePropertyName("Outputs");
        writer.WriteStartObject();
        foreach (var (key, outputValue) in value.Outputs)
        {
            writer.WritePropertyName(key);
            JsonSerializer.Serialize(writer, outputValue, options);
        }
        writer.WriteEndObject();
        
        // Write dependencies
        writer.WritePropertyName("Dependencies");
        writer.WriteStartArray();
        foreach (var dependency in value.Dependencies)
        {
            writer.WriteStringValue(dependency);
        }
        writer.WriteEndArray();
        
        writer.WriteNumber("Priority", value.Priority);
        writer.WriteString("CreatedAt", value.CreatedAt.ToString("O")); // ISO 8601 format
        
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