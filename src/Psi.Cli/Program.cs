using System.CommandLine;
// using System.CommandLine.Invocation; // Not needed for v2
using System.Text.Json;
using System.Text.Json.Serialization;
using Aevatar.Workshop.Client;
using Aevatar.Core.Abstractions;
using PsiAgent;
using PsiOrleans.Common.Models;

class Program
{
    private const string CachePath = "~/.psigagent_cache";

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("PsiGAgent Command Line Tool");

        var createTaskArg = new Argument<string>("task", "Task description");
        var createCommand = new Command("create", "Create a PsiGAgent and pass a task")
        {
            createTaskArg
        };
        createCommand.SetHandler(async (string task) =>
        {
            var (gAgentFactory, _) = await InitAsync();
            await CreateAgentAndCacheAsync(gAgentFactory, task);
        }, createTaskArg);

        var listCommand = new Command("list", "View list of created agents");
        listCommand.SetHandler(async () => { await ListCachedAgentsAsync(); });

        var stateIdArg = new Argument<string?>("id", () => null, "Agent ID (optional, defaults to last cached)");
        var stateCommand = new Command("state", "View state of a selected agent by ID")
        {
            stateIdArg
        };
        stateCommand.SetHandler(async (string? id) =>
        {
            if (string.IsNullOrEmpty(id))
            {
                var ids = await ReadCacheAsync();
                if (ids.Count == 0)
                {
                    Console.WriteLine("No cached agents found. Please create an agent first or specify an ID.");
                    return;
                }

                id = ids[^1]; // last cached id
            }

            var (gAgentFactory, _) = await InitAsync();
            await ViewAgentStateAsync(gAgentFactory, id);
        }, stateIdArg);

        var clearCacheCommand = new Command("clear", "Clear the cached agent IDs");
        clearCacheCommand.SetHandler(async () => { await ClearCacheAsync(); });

        var continueMessageArg = new Argument<string>("message", "User message to continue the conversation");
        var continueCommand = new Command("continue", "Send a ContinueConversationEvent to the last cached agent")
        {
            continueMessageArg
        };
        continueCommand.SetHandler(async (string message) =>
        {
            var ids = await ReadCacheAsync();
            if (ids.Count == 0)
            {
                Console.WriteLine("No cached agents found. Please create an agent first.");
                return;
            }
            var id = ids[^1]; // last cached id
            var (gAgentFactory, _) = await InitAsync();
            var agent = await gAgentFactory.GetGAgentAsync(GrainId.Parse(id));
            var publisher = await gAgentFactory.GetGAgentAsync<IPublishingGAgent>(Guid.NewGuid());
            var evt = new ContinueConversationEvent { UserMessage = message };
            await publisher.PublishEventAsync(evt, agent);
            Console.WriteLine($"Sent ContinueConversationEvent to agent {id} with message: {message}");
        }, continueMessageArg);

        rootCommand.AddCommand(createCommand);
        rootCommand.AddCommand(listCommand);
        rootCommand.AddCommand(stateCommand);
        rootCommand.AddCommand(clearCacheCommand);
        rootCommand.AddCommand(continueCommand);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<(IGAgentFactory, IServiceProvider)> InitAsync()
    {
        var serviceProvider = await Startup.RunAsync(Array.Empty<string>());
        var gAgentFactory = (IGAgentFactory)serviceProvider.GetService(typeof(IGAgentFactory));
        return (gAgentFactory, serviceProvider);
    }

    static string ExpandPath(string path)
    {
        if (path.StartsWith("~"))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return path;
    }

    static async Task<List<string>> ReadCacheAsync()
    {
        var path = ExpandPath(CachePath);
        if (!File.Exists(path)) return new List<string>();
        var json = await File.ReadAllTextAsync(path);
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    static async Task WriteCacheAsync(List<string> ids)
    {
        var path = ExpandPath(CachePath);
        var dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(ids);
        await File.WriteAllTextAsync(path, json);
    }

    static async Task CreateAgentAndCacheAsync(IGAgentFactory gAgentFactory, string task)
    {
        var psi = await gAgentFactory.GetGAgentAsync("psi", "psi");
        var publisher = await gAgentFactory.GetGAgentAsync<IPublishingGAgent>(Guid.NewGuid());
        var config = GetAgentConfiguration();
        await publisher.PublishEventAsync(new SendConfigEvent { Configuration = config, ParenteAgentId = string.Empty },
            psi);
        var callId = Guid.NewGuid().ToString();
        await publisher.PublishEventAsync(new SendTaskEvent { CallId = callId, Task = task }, psi);
        var id = psi.GetGrainId().ToString();
        var ids = await ReadCacheAsync();
        if (!ids.Contains(id)) ids.Add(id);
        await WriteCacheAsync(ids);
        Console.WriteLine($"Created PsiGAgent with ID: {id} and passed task.");
    }

    static async Task ListCachedAgentsAsync()
    {
        var ids = await ReadCacheAsync();
        if (ids.Count == 0)
        {
            Console.WriteLine("No agents cached.");
            return;
        }

        Console.WriteLine("Cached PsiGAgent IDs:");
        for (int i = 0; i < ids.Count; i++)
            Console.WriteLine($"{i + 1}. {ids[i]}");
    }

    static async Task ViewAgentStateAsync(IGAgentFactory gAgentFactory, string id)
    {
        var guid = GrainId.Parse(id);
        var visited = new HashSet<string>();
        var agentStates = await GetAgentStatesRecursive(gAgentFactory, guid, visited);
        PrintAgentStates(agentStates);
    }

    static async Task<List<(AgentState state, int depth, string id)>> GetAgentStatesRecursive(
        IGAgentFactory gAgentFactory, GrainId agentId, HashSet<string> visited, int depth = 0)
    {
        var result = new List<(AgentState, int, string)>();
        if (!visited.Add(agentId.ToString()))
        {
            return result;
        }

        try
        {
            var psi = await gAgentFactory.GetGAgentAsync<IStateGAgent<AgentState>>(agentId);
            var state = (AgentState)await psi.GetStateAsync();
            var redactedState = RedactModelConfiguration(state);
            result.Add((redactedState, depth, agentId.ToString()));
            if (state.Children != null && state.Children.Count > 0)
            {
                foreach (var childId in state.Children)
                {
                    var childStates = await GetAgentStatesRecursive(gAgentFactory, childId, visited, depth + 1);
                    result.AddRange(childStates);
                }
            }
        }
        catch
        {
            // Optionally, add error handling or logging here
        }

        return result;
    }

    static void PrintAgentStates(List<(AgentState state, int depth, string id)> agentStates)
    {
        var output = agentStates.Select(x => new { id = x.id, depth = x.depth, state = x.state }).ToList();
        var json = JsonSerializer.Serialize(output);
        Console.WriteLine(json);
    }

    static AgentState RedactModelConfiguration(AgentState state)
    {
        // Deep clone AgentState (shallow for all except config)
        var clone = new AgentState
        {
            AgentId = state.AgentId,
            ParentAgentId = state.ParentAgentId,
            Task = state.Task,
            SpecializedAgentId = state.SpecializedAgentId,
            CallId = state.CallId,
            AgentRole = state.AgentRole,
            Orchestrator = state.Orchestrator,
            TaskAnalysisResult = state.TaskAnalysisResult,
            SpecializedState = state.SpecializedState
        };
        if (state.Configuration != null)
        {
            var config = state.Configuration;
            var model = config.Model;
            var redactedModel = new ModelConfiguration
            {
                ModelId = model?.ModelId ?? string.Empty,
                ApiKey = "***",
                BaseUrl = model?.BaseUrl != null ? "***" : null,
                DeploymentName = model?.DeploymentName != null ? "***" : null,
                Endpoint = model?.Endpoint != null ? "***" : null,
                ApiVersion = model?.ApiVersion != null ? "***" : null
            };
            clone.Configuration = new AgentConfiguration
            {
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens,
                Model = redactedModel
            };
        }

        return clone;
    }

    static async Task ClearCacheAsync()
    {
        var path = ExpandPath(CachePath);
        if (File.Exists(path))
        {
            File.Delete(path);
            Console.WriteLine("Cache cleared.");
        }
        else
        {
            Console.WriteLine("No cache file found.");
        }
    }

    static AgentConfiguration GetAgentConfiguration()
    {
        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        var azureApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME");
        var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var hasAzureConfig = !string.IsNullOrEmpty(azureEndpoint) && !string.IsNullOrEmpty(azureApiKey) &&
                             !string.IsNullOrEmpty(azureDeployment);
        var hasOpenAiConfig = !string.IsNullOrEmpty(openAiApiKey);
        if (hasAzureConfig)
        {
            var modelConfig = new ModelConfiguration
            {
                ModelId = azureDeployment ?? "gpt-4o-mini",
                ApiKey = azureApiKey,
                BaseUrl = azureEndpoint,
                DeploymentName = azureDeployment,
                Endpoint = azureEndpoint,
                ApiVersion = null
            };
            return new AgentConfiguration
            {
                Temperature = 0.7,
                MaxTokens = 1000,
                Model = modelConfig
            };
        }
        else
        {
            var modelConfig = new ModelConfiguration
            {
                ModelId = azureDeployment ?? "gpt-4o-mini",
                ApiKey = azureApiKey,
                BaseUrl = azureEndpoint,
                DeploymentName = azureDeployment,
                Endpoint = azureEndpoint,
                ApiVersion = null
            };
            return new AgentConfiguration
            {
                Temperature = 0,
                MaxTokens = 0,
                Model = modelConfig
            };
        }
    }
}