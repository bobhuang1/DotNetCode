using BlazorSignalR.Components;
using BlazorSignalR.Hubs;
using BlazorSignalR.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// SignalR hub + the hosted telemetry broadcaster it uses.
builder.Services.AddSignalR();
builder.Services.AddSingleton<RealtimeHubBridge>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RealtimeHubBridge>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
