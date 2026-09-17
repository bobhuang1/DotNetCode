# Blazor Web App with SignalR (.NET Core 10)

A **.NET 10 Blazor Web App** using interactive server render mode, plus an
explicit **SignalR hub** that pushes realtime updates to every connected
browser. It shows the two ways realtime content reaches the page: server
generated (a hosted service emits telemetry) and user generated (one browser
broadcasts to all).

| Project | What it is |
| --- | --- |
| `BlazorSignalR/` | The whole sample – Blazor Web App, `NotificationHub`, hosted telemetry broadcaster |

## Run it

```powershell
dotnet run --project BlazorSignalR
```

Open the app, then:

1. Go to **Realtime (SignalR)** – every three seconds a hosted service pushes a
   telemetry reading to the page through the hub.
2. Open the same page in a second tab and type a message – every connected tab
   receives it.

## Where the pieces live

- `Hubs/NotificationHub.cs` – SignalR hub at `/hubs/notifications`. Tracks
  attached clients and relays `BroadcastFromUserAsync` messages to everyone.
- `Services/RealtimeHubBridge.cs` – a `BackgroundService` that keeps the list
  of connected `ISingleClientProxy` instances and broadcasts a
  `TelemetryUpdate` to all of them every three seconds. Registering the bridge
  once as a singleton, and also as a hosted service, gives the hub and the push
  loop a single shared source of truth.
- `Components/Pages/Realtime.razor` – an interactive page that opens its own
  connection with `Microsoft.AspNetCore.SignalR.Client`
  (`HubConnectionBuilder`), shows a connection status card, automatically
  reconnects, and sends hub invocations from the UI.
- `Program.cs` – `AddSignalR()`, `MapHub<NotificationHub>("/hubs/notifications")`,
  and the bridge registration.

## Notes

- Blazor Server rides on SignalR already; this sample adds a *separate*
  application-level hub rather than leaning on the Blazor circuit. That way the
  same hub can also be consumed by Blazor WebAssembly clients, JS, or mobile.
- The Blazor Server connection is made from the server-side circuit, so only
  `Microsoft.AspNetCore.SignalR.Client` (part of the shared framework) is
  needed – no npm. For a browser-side connection use the `@microsoft/signalr`
  JS client and a `.js` file under `wwwroot` instead.
- Telemetry is in-process-only, so it works for this single-app sample. When
  the broadcaster must survive app restarts or scale beyond one instance, back
  it with Azure SignalR Service / a service bus, or scale-out SignalR with a
  Redis backplane.
- No auth is configured on the hub; secure it the way the rest of the app is
  authenticated in a real deployment.