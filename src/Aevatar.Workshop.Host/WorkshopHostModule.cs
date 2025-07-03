using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using PsiGAgent.Common.Interfaces;
using PsiGAgent.Plugins;
using PsiGAgent.Plugins.Services;
using Serilog;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace Aevatar.Workshop.Host;

[DependsOn(
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAutofacModule),
    typeof(AbpAutoMapperModule),
    typeof(AevatarModule)
)]
public class WorkshopHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<WorkshopHostModule>(); });
        context.Services.AddHostedService<AevatarWorkshopHostedService>();
        context.Services.AddSerilog(_ => { },
            true, writeToProviders: true);
        context.Services.AddHttpClient();
        context.Services.AddSingleton<IEventDispatcher, DefaultEventDispatcher>();
        context.Services.AddSingleton<IKernelFactory, KernelFactory>();
        context.Services.AddSingleton<IKernelFunctionRegistry, KernelFunctionRegistry>();
        
        // Register web search services
        context.Services.AddHttpClient<WebContentFetcher>();
        context.Services.AddSingleton<IWebContentFetcher, WebContentFetcher>();
        
        // Register all search engines
        context.Services.AddSingleton<ISearchEngine, GoogleSearchEngine>(); // GoogleSearchEngine now uses built-in GoogleTextSearch
        context.Services.AddHttpClient<DuckDuckGoSearchEngine>();
        context.Services.AddHttpClient<BingSearchEngine>();
        context.Services.AddSingleton<ISearchEngine, DuckDuckGoSearchEngine>();
        context.Services.AddSingleton<ISearchEngine, BingSearchEngine>();
        
        // Register main web search service
        context.Services.AddSingleton<IWebSearchService, WebSearchService>();
    }
}