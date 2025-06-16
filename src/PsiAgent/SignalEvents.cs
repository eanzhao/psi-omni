using Aevatar.Core.Abstractions;
using PsiOrleans.Common.Models;

namespace PsiAgent;

[GenerateSerializer]
public class TaskAnalysisDone : EventBase
{
}

[GenerateSerializer]
public class TaskSet : EventBase
{
}

[GenerateSerializer]
public class SpecializedRunDone : EventBase
{
    [Id(0)] private List<ChatMessage> ChatHistory { get; set; }
}