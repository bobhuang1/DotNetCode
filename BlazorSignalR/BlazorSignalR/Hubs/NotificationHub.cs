using BlazorSignalR.Services;
using Microsoft.AspNetCore.SignalR;

namespace BlazorSignalR.Hubs;

/// <summary>
/// SignalR hub for the realtime sample. The bridge distributes server-generated
/// telemetry to every attached connection; this hub also relays user messages
/// sent by one browser to all connected browsers.
/// </summary>
public sealed class NotificationHub : Hub
{
    private readonly RealtimeHubBridge _bridge;

    public NotificationHub(RealtimeHubBridge bridge) => _bridge = bridge;

    public override Task OnConnectedAsync()
    {
        _bridge.Attach(Context.ConnectionId, Clients.Caller);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _bridge.Detach(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>Called by a connected client: relays the message to everyone.</summary>
    public Task BroadcastFromUserAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.CompletedTask;
        }

        return _bridge.SendToAllAsync("userMessage", Context.ConnectionId, message.Trim());
    }
}