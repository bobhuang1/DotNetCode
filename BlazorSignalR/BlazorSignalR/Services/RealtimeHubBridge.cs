using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace BlazorSignalR.Services;

/// <summary>A point-in-time reading pushed to connected SignalR clients.</summary>
public sealed record TelemetryUpdate(DateTimeOffset Timestamp, string Label, int Value);

/// <summary>
/// Tracks every connection attached to <see cref="Hubs.NotificationHub"/> and
/// broadcasts server-generated telemetry to all of them. Runs as a hosted
/// service so the push layer is independent of any single HTTP request.
/// </summary>
public sealed class RealtimeHubBridge : BackgroundService
{
    private static readonly string[] Labels = { "cpu", "memory", "requests" };

    private readonly ConcurrentDictionary<string, ISingleClientProxy> _clients = new();
    private readonly ILogger<RealtimeHubBridge> _logger;
    private int _counter;

    public RealtimeHubBridge(ILogger<RealtimeHubBridge> logger) => _logger = logger;

    public void Attach(string connectionId, ISingleClientProxy client)
    {
        _clients[connectionId] = client;
        _logger.LogInformation("Client attached: {ConnectionId} (total {Count})", connectionId, _clients.Count);
    }

    public void Detach(string connectionId)
    {
        if (_clients.TryRemove(connectionId, out _))
        {
            _logger.LogInformation("Client detached: {ConnectionId} (total {Count})", connectionId, _clients.Count);
        }
    }

    public Task SendToAllAsync(string method, params object?[] args)
    {
        var targets = _clients.Values.ToArray();
        return targets.Length == 0 ? Task.CompletedTask : Task.WhenAll(targets.Select(c => c.SendCoreAsync(method, args)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            var index = Interlocked.Increment(ref _counter) % Labels.Length;
            var update = new TelemetryUpdate(DateTimeOffset.UtcNow, Labels[index], Random.Shared.Next(0, 101));

            if (!_clients.IsEmpty)
            {
                await SendToAllAsync("telemetry", update);
            }
        }
    }
}