using Microsoft.SemanticKernel;

namespace Aevatar.Workshop.Host;

public static class DemoPlugin
{
    [KernelFunction("AddNumbers")]
    public static Task<int> AddNumbersAsync(int a, int b)
        => Task.FromResult(a + b);

    [KernelFunction("GetUSGDP2024")]
    public static Task<double> GetUSGDP2024Async()
        => Task.FromResult(28500000.0); // 单位：百万美元（示例数据）

    [KernelFunction("GetNYGDP2024")]
    public static Task<double> GetNYGDP2024Async()
        => Task.FromResult(2200000.0); // 单位：百万美元（示例数据）
    [KernelFunction("GetCAGDP2024")]
    public static Task<double> GetCAGDP2024Async()
        => Task.FromResult(4100000.0); // 单位：百万美元（示例数据）

    [KernelFunction("CalculatePercentage")]
    public static Task<double> CalculatePercentageAsync(double part, double whole)
        => Task.FromResult(whole != 0 ? (part / whole) * 100.0 : 0.0);
}