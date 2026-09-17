using McpServerLibrary;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Key Vault-backed credentials + generic downstream API client, shared with the other hosts.
// Values come from local.settings.json locally and app settings in Azure.
builder.Services.AddMcpServerLibrary(settings =>
{
    settings.VaultUrl = GetSetting("MCP_VAULT_URL");
    settings.DownstreamApiBaseUrl = GetSetting("MCP_DOWNSTREAM_API_BASE_URL");
    settings.UserNameSecretName = GetSetting("MCP_USERNAME_SECRET_NAME") is { Length: > 0 } userName ? userName : settings.UserNameSecretName;
    settings.ApiSecretSecretName = GetSetting("MCP_APISECRET_SECRET_NAME") is { Length: > 0 } apiSecret ? apiSecret : settings.ApiSecretSecretName;
});

builder.Build().Run();

static string GetSetting(string name) => Environment.GetEnvironmentVariable(name) ?? string.Empty;