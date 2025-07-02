using Microsoft.Extensions.Logging;
using PsiGAgent.Common;
using Aevatar.Core.Abstractions;

namespace PsiGAgent.Omni;

public partial class PsiOmniGAgent
{
    [EventHandler]
    public async Task HandleSendConfigEventAsync(AgentConfigEvent @event)
    {
        Logger.LogInformation("SendConfigEvent: {Task}", @event.Configuration.Model.ModelId);
        RaiseEvent(new UpdateSendConfigEvent()
        {
            Event = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleUserMessageEventAsync(UserMessageEvent @event)
    {
        if (@event.TargetAgentId != this.GetGrainId().ToString())
        {
            // Not for me
            return;
        }

        if (_receivedMessageIds.Contains(@event.UniqueId))
        {
            return;
        }

        _receivedMessageIds.Add(@event.UniqueId);
        RaiseEvent(new ReceiveUserMessageEvent()
        {
            Event = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleAgentMessageEventAsync(AgentMessageEvent @event)
    {
        if (@event.TargetAgentId != this.GetGrainId().ToString())
        {
            // Not for me
            return;
        }

        if (_receivedMessageIds.Contains(@event.UniqueId))
        {
            return;
        }

        _receivedMessageIds.Add(@event.UniqueId);

        RaiseEvent(new ReceiveAgentMessageEvent()
        {
            Event = @event
        });
        await ConfirmEvents();
    }

    [EventHandler]
    public async Task HandleSelfReportEventAsync(SelfReportEvent @event)
    {
        if (@event.TargetAgentId != this.GetGrainId().ToString())
        {
            // Not for me
            return;
        }

        if (_receivedMessageIds.Contains(@event.UniqueId))
        {
            return;
        }

        _receivedMessageIds.Add(@event.UniqueId);

        RaiseEvent(new UpdateChildEvent()
        {
            LastChildDescriptor = @event.SelfReport
        });
        await ConfirmEvents();
    }
}