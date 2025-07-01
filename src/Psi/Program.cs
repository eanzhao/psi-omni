using System.CommandLine;
using System.Text.Json;
using Aevatar.Workshop.Client;
using Aevatar.Core.Abstractions;
using Orleans;
using Orleans.Runtime;
using PsiGAgent.Common;
using PsiGAgent.Common.Models;
using PsiGAgent.Omni;

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
        var continueIdArg = new Argument<string?>("id", () => null, "Agent ID (optional, defaults to last cached)");
        var continueCommand = new Command("continue", "Send a ContinueConversationEvent to the specified or last cached agent")
        {
            continueMessageArg,
            continueIdArg
        };
        continueCommand.SetHandler(async (string message, string? id) =>
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
            var agent = await gAgentFactory.GetGAgentAsync(GrainId.Parse(id));
            var publisher = await gAgentFactory.GetGAgentAsync<IPublishingGAgent>(Guid.NewGuid());
            var evt = new UserMessageEvent
            {
                TargetAgentId = id,
                CallId = Guid.NewGuid().ToString(),
                Content = message,
                ReplyToAgentId = null
            };
            await publisher.PublishEventAsync(evt, agent);
            Console.WriteLine($"Sent ContinueConversationEvent to agent {id} with message: {message}");
        }, continueMessageArg, continueIdArg);

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
        var psi = await gAgentFactory.GetGAgentAsync("psi", "omni");
        var publisher = await gAgentFactory.GetGAgentAsync<IPublishingGAgent>(Guid.NewGuid());
        var config = GetAgentConfiguration();
        await publisher.PublishEventAsync(new AgentConfigEvent
            {
                Configuration = config,
                Tools = []
            },
            psi);
        var callId = Guid.NewGuid().ToString();
        var id = psi.GetGrainId().ToString();
        await publisher.PublishEventAsync(new UserMessageEvent
        {
            TargetAgentId = id,
            CallId = callId,
            Content = task
        }, psi);

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

    static async Task<List<(PsiOmniGAgentState state, int depth, string id)>> GetAgentStatesRecursive(
        IGAgentFactory gAgentFactory, GrainId agentId, HashSet<string> visited, int depth = 0)
    {
        var result = new List<(PsiOmniGAgentState, int, string)>();
        if (!visited.Add(agentId.ToString()))
        {
            return result;
        }

        try
        {
            var psi = await gAgentFactory.GetGAgentAsync<IStateGAgent<PsiOmniGAgentState>>(agentId);
            var state = (PsiOmniGAgentState)await psi.GetStateAsync();
            var redactedState = RedactModelConfiguration(state);
            result.Add((redactedState, depth, agentId.ToString()));
            if (state.ChildAgents != null && state.ChildAgents.Count > 0)
            {
                foreach (var (childId, child) in state.ChildAgents)
                {
                    var childStates =
                        await GetAgentStatesRecursive(gAgentFactory, GrainId.Parse(childId), visited, depth + 1);
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

    // static async Task<List<(PsiOmniGAgentState state, int depth, string id)>> GetAgentStatesRecursive(
    //     IGAgentFactory gAgentFactory, GrainId agentId, HashSet<string> visited, int depth = 0)
    // {
    //     var result = new List<(CompositeState, int, string)>();
    //     if (!visited.Add(agentId.ToString()))
    //     {
    //         return result;
    //     }
    //
    //     try
    //     {
    //         if (agentId.ToString().Contains("orchestrator"))
    //         {
    //             var psi = await gAgentFactory.GetGAgentAsync<IStateGAgent<PsiOrchestratorGAgentState>>(agentId);
    //             var state = await psi.GetStateAsync();
    //             var compositeState = new CompositeState { OrchestratorState = state };
    //             var redactedState = RedactModelConfiguration(compositeState);
    //             result.Add((redactedState, depth, agentId.ToString()));
    //             if (state.ChildAgents != null && state.ChildAgents.Count > 0)
    //             {
    //                 foreach (var child in state.ChildAgents)
    //                 {
    //                     var childStates = await GetAgentStatesRecursive(gAgentFactory, GrainId.Parse(child.AgentId), visited, depth + 1);
    //                     result.AddRange(childStates);
    //                 }
    //             }
    //             // Note: Children are not on PsiOrchestratorGAgentState, so we can't recurse down.
    //             // This might be a design choice or an omission. For now, we follow the type definition.
    //         }
    //         else
    //         {
    //             var psi = await gAgentFactory.GetGAgentAsync<IStateGAgent<PsiSpecializedGAgentState>>(agentId);
    //             var state = await psi.GetStateAsync();
    //             var compositeState = new CompositeState { SpecializedState = state };
    //             var redactedState = RedactModelConfiguration(compositeState);
    //             result.Add((redactedState, depth, agentId.ToString()));
    //         }
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error getting state for {agentId}: {ex.Message}");
    //     }
    //
    //     return result;
    // }

    static void PrintAgentStates(List<(PsiOmniGAgentState state, int depth, string id)> agentStates)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var output = agentStates.Select(x => new { id = x.id, depth = x.depth, state = x.state }).ToList();
        var json = JsonSerializer.Serialize(output, options);
        Console.WriteLine(json);
    }

    static PsiOmniGAgentState RedactModelConfiguration(PsiOmniGAgentState state)
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
        state.Configuration.Model = redactedModel;

        return state;
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