using Aevatar.Core.Abstractions;

namespace PsiGAgent.Omni;

[GenerateSerializer]
public class PsiOmniGAgentConfig : ConfigurationBase
{
    [Id(0)] public int Depth { get; set; } = 0;
}