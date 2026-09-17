using McpServerLibrary;
using McpServerLibrary.Configuration;
using McpServerLibrary.KeyVault;
using McpServerLibrary.Tools;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Key Vault-backed credentials + generic downstream API client, shared with the other hosts.
builder.Services.AddMcpServerLibrary(builder.Configuration);

// Expose the shared [McpServerTool] methods over the Streamable HTTP transport at /mcp.
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<GenericApiTools>();

var app = builder.Build();

// Gate the MCP endpoint behind an API key whose value lives in Key Vault.
// When KeyVault:VaultUrl is empty the check is skipped so the sample runs locally
// without a vault - never rely on that in a real deployment.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<KeyVaultSettings>>().Value;

        if (!string.IsNullOrWhiteSpace(settings.VaultUrl))
        {
            var keyVault = context.RequestServices.GetRequiredService<IKeyVaultCredentialProvider>();
            var expected = await keyVault.GetSecretAsync(settings.McpClientApiKeySecretName);

            if (!string.Equals(context.Request.Headers["x-api-key"].ToString(), expected, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
    }

    await next(context);
});

app.MapMcp("/mcp");

app.Run();