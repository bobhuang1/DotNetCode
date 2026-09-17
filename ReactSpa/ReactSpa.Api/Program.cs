using ReactSpa.Api;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/weather", () =>
{
    var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
    return Results.Ok(Enumerable.Range(1, 5).Select(i => new WeatherForecast(
        DateOnly.FromDateTime(DateTime.Now.AddDays(i)),
        Random.Shared.Next(-20, 55),
        summaries[Random.Shared.Next(summaries.Length)])));
});

app.MapPost("/api/echo", (EchoRequest request) => Results.Ok(new EchoResponse(request.Message)));

// Serve the compiled SPA from wwwroot when the front end has been built
// (either `npm run build`/`dotnet run`, or `dotnet publish`). In dev, the Vite
// dev server proxies /api to this host, so the SPA is served from 5173 instead.
var spaIndex = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "index.html");
if (File.Exists(spaIndex))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();