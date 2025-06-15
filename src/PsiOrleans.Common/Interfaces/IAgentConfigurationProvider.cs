using PsiOrleans.Common.Models;

namespace PsiOrleans.Common.Interfaces
{
    /// <summary>
    /// Provides access to AgentConfiguration for agent context consumers.
    /// </summary>
    public interface IAgentConfigurationProvider
    {
        AgentConfiguration AgentConfiguration { get; }
    }
} 