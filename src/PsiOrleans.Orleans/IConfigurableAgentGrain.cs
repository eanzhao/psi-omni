using System.Threading.Tasks;
using PsiOrleans.Common.Models;
using PsiOrleans.Specialized.Services;

namespace PsiOrleans.Orleans
{
    /// <summary>
    /// Orleans 粒子适配层接口，桥接外部请求与核心 packages。
    /// 支持任务处理、初始化、回调、状态获取等核心方法。
    /// </summary>
    public interface IConfigurableAgentGrain
    {
        /// <summary>
        /// 处理任务请求，支持 parentId 回调链。
        /// </summary>
        Task<string> ProcessTaskAsync(string task, string? callId);

        /// <summary>
        /// 初始化代理配置与工具。
        /// </summary>
        Task<bool> InitializeAsync(AgentConfiguration config, string[] toolNames);

        /// <summary>
        /// 接收子代理或工具的回调。
        /// </summary>
        Task ReceiveCallbackAsync(string callId, string message, bool isSuccess);

        /// <summary>
        /// 获取当前代理状态（可序列化）。
        /// </summary>
        Task<object> GetStateAsync();

        /// <summary>
        /// 判断代理是否已初始化。
        /// </summary>
        Task<bool> IsInitializedAsync();
    }
} 