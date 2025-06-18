using System.Net;
using Aevatar.Core.Abstractions;
using Aevatar.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Configuration;
using Orleans.Networking.Shared;

namespace Aevatar.Workshop.Host;

public static class OrleansHostExtension
{
    public static IHostBuilder UseOrleansConfiguration(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseOrleans((context, siloBuilder) =>
            {
                siloBuilder
                    .UseLocalhostClustering()
                    .Configure<EndpointOptions>(options =>
                    {
                        options.GatewayPort = 30000;
                        options.SiloPort = 11111;
                        options.AdvertisedIPAddress = IPAddress.Parse("127.0.0.1");
                        options.GatewayListeningEndpoint = new IPEndPoint(IPAddress.Any, 30000); // Explicitly bind
                        options.SiloListeningEndpoint = new IPEndPoint(IPAddress.Any, 11111);
                    })
                    .Configure<SocketConnectionOptions>(options =>
                    {
                        options.NoDelay = true; // Disable Nagle algorithm (default, but explicit)
                        options.KeepAlive = false; // Disable keep-alive to prevent socket closure
                        options.KeepAliveTimeSeconds = 90; // Adjust if KeepAlive is enabled
                        options.KeepAliveIntervalSeconds = 30;
                        options.KeepAliveRetryCount = 10;
                    })
                    .AddMemoryGrainStorage("Default")
                    .AddMemoryStreams(AevatarCoreConstants.StreamProvider)
                    .AddMemoryGrainStorage("PubSubStore")
                    .AddLogStorageBasedLogConsistencyProvider()
                    .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug).AddConsole(); })
                    .UseAevatar()
                    .UseDashboard()
                    ;
            })
            .UseConsoleLifetime();
    }
}