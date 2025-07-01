using Aevatar.Core.Abstractions;
using PsiOrleans.Common;
using PsiOrleans.Common.Models;

namespace Aevatar.Workshop.Client;

public static class PsiGAgentDemo
{
    public static async Task RunAsync(IGAgentFactory gAgentFactory)
    {
        var psi = await gAgentFactory.GetGAgentAsync("psi", "psi");
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
        var task = "Calculate what percentage of US GDP was contributed by New York state in 2024";
        await publisher.PublishEventAsync(new UserMessageEvent
        {
            TargetAgentId = id,
            CallId = callId,
            Content = task
        }, psi);


        Console.WriteLine("Published.");
    }

    private static AgentConfiguration GetAgentConfiguration()
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