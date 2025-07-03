using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PsiGAgent.Plugins;
using PsiGAgent.Plugins.Services;
using DotNetEnv;

namespace WebSearchTest;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine(Directory.GetCurrentDirectory());
        // Load environment variables from .env file
        var envPath = "../../../../../.env";
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
            Console.WriteLine($"✅ Loaded environment variables from: {envPath}");
        }
        else
        {
            Console.WriteLine($"❌ .env file not found at: {envPath}");
            Console.WriteLine("Please ensure GOOGLE_API_KEY and GOOGLE_SEARCH_ENGINE_ID are set");
        }

        // Display environment variables (without exposing API key)
        var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        var googleSearchEngineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID");
        
        Console.WriteLine($"GOOGLE_API_KEY: {(string.IsNullOrEmpty(googleApiKey) ? "❌ Not set" : "✅ Set")}");
        Console.WriteLine($"GOOGLE_SEARCH_ENGINE_ID: {googleSearchEngineId ?? "❌ Not set"}");
        Console.WriteLine();

        // Build host with DI
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                // Add logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // Add HTTP client
                services.AddHttpClient();

                // Register search engines
                services.AddScoped<ISearchEngine, GoogleSearchEngine>();
                services.AddScoped<IWebContentFetcher, WebContentFetcher>();
                services.AddScoped<IWebSearchService, WebSearchService>();
                services.AddScoped<WebSearchPlugin>();
            })
            .Build();

        // Get the WebSearchPlugin from DI
        var webSearchPlugin = host.Services.GetRequiredService<WebSearchPlugin>();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        logger.LogInformation("🚀 Starting WebSearch Plugin Test");
        Console.WriteLine("==========================================");
        Console.WriteLine("WebSearch Plugin Test");
        Console.WriteLine("==========================================");

        // Test queries
        var testQueries = new[]
        {
            "artificial intelligence news 2024",
            "Microsoft .NET 9 features",
            "OpenAI ChatGPT latest updates"
        };

        foreach (var query in testQueries)
        {
            Console.WriteLine($"\n🔍 Testing query: '{query}'");
            Console.WriteLine("------------------------------------------");
            
            try
            {
                // Test ExecuteAsync with basic parameters
                var result = await webSearchPlugin.ExecuteAsync(
                    query: query,
                    numResults: 3,
                    lang: "en",
                    country: "us",
                    fetchContent: false);

                Console.WriteLine("✅ ExecuteAsync completed");
                Console.WriteLine($"📄 Result length: {result.Length} characters");
                
                // Pretty print the result
                Console.WriteLine("📋 Result:");
                Console.WriteLine(result);
                
                // Test QuickSearch as well
                Console.WriteLine("\n🔍 Testing QuickSearch for same query...");
                var quickResult = await webSearchPlugin.QuickSearchAsync(query);
                Console.WriteLine("✅ QuickSearch completed");
                Console.WriteLine($"📄 Quick result length: {quickResult.Length} characters");
                Console.WriteLine("📋 Quick Result:");
                Console.WriteLine(quickResult);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error testing query '{query}': {ex.Message}");
                logger.LogError(ex, "Error testing query: {Query}", query);
            }
            
            Console.WriteLine("\n" + new string('=', 50));
        }

        Console.WriteLine("\n🏁 Test completed!");
    }
}