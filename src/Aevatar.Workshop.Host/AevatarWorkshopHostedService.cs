using Microsoft.Extensions.Hosting;
using Volo.Abp;
using PsiOrleans.Plugins;
using PsiOrleans.Common.Interfaces;
using Microsoft.SemanticKernel; // For KernelFunction, KernelPluginFactory, KernelFunctionFactory
using Aevatar.Workshop.Host; // For DemoPlugin

namespace Aevatar.Workshop.Host;

public class AevatarWorkshopHostedService : IHostedService
{
    private readonly IAbpApplicationWithExternalServiceProvider _application;
    private readonly IServiceProvider _serviceProvider;

    public AevatarWorkshopHostedService(
        IAbpApplicationWithExternalServiceProvider application,
        IServiceProvider serviceProvider)
    {
        _application = application;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _application.InitializeAsync(_serviceProvider);
        var functionRegistry = _serviceProvider.GetService(typeof(IKernelFunctionRegistry)) as IKernelFunctionRegistry;
        if (functionRegistry != null)
        {
            MathFunctionRegistration.RegisterAllMathFunctions(functionRegistry);

            // Register DemoPlugin functions
            var demoFunctions = new List<KernelFunction>
            {
                KernelFunctionFactory.CreateFromMethod((int a, int b) => DemoPlugin.AddNumbersAsync(a, b), "AddNumbers", "Add two numbers"),
                KernelFunctionFactory.CreateFromMethod(() => DemoPlugin.GetUSGDP2024Async(), "GetUSGDP2024", "Get US GDP for 2024"),
                KernelFunctionFactory.CreateFromMethod(() => DemoPlugin.GetNYGDP2024Async(), "GetNYGDP2024", "Get New York GDP for 2024"),
                KernelFunctionFactory.CreateFromMethod((double part, double whole) => DemoPlugin.CalculatePercentageAsync(part, whole), "CalculatePercentage", "Calculate percentage of part over whole")
            };
            var demoPlugin = KernelPluginFactory.CreateFromFunctions("DemoPlugin", demoFunctions);
            functionRegistry.RegisterPlugin("DemoPlugin", demoPlugin);
            functionRegistry.RegisterFunction("AddNumbers", demoPlugin["AddNumbers"]);
            functionRegistry.RegisterFunction("GetUSGDP2024", demoPlugin["GetUSGDP2024"]);
            functionRegistry.RegisterFunction("GetNYGDP2024", demoPlugin["GetNYGDP2024"]);
            functionRegistry.RegisterFunction("CalculatePercentage", demoPlugin["CalculatePercentage"]);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _application.Shutdown();
        return Task.CompletedTask;
    }
}