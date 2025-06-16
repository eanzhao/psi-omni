using Aevatar.Core.Abstractions;
using PsiAgent;
using PsiOrleans.Common.Models;

namespace Aevatar.Workshop.Client;

public static class PsiGAgentDemo
{
    public static async Task RunAsync(IGAgentFactory gAgentFactory)
    {
        var psi = await gAgentFactory.GetGAgentAsync("psi", "psi");
        var publisher = await gAgentFactory.GetGAgentAsync<IPublishingGAgent>(Guid.NewGuid());

        await publisher.PublishEventAsync(new SendConfigEvent
        {
            Configuration = GetAgentConfiguration(),
            ParenteAgentId = String.Empty
        }, psi);
        
        // await publisher.PublishEventAsync(new SendTaskEvent
        // {
        //     CallId = Guid.NewGuid().ToString(),
        //     Task = "percentage of 2 over 24"
        // }, psi);

        await publisher.PublishEventAsync(new SendTaskEvent
        {
            CallId = Guid.NewGuid().ToString(),
            Task = "Find US and New York state GDP in 2024. Calculate what percentage of US GDP was New York state."
        }, psi);
        
        await publisher.PublishEventAsync(new PingEvent(), psi);


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
                SystemPrompt = null,
                AgentName = null,
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
                SystemPrompt = null,
                AgentName = null,
                Temperature = 0,
                MaxTokens = 0,
                Model = modelConfig
            };
        }
    }
}