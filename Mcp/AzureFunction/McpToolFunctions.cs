using McpServerLibrary.KeyVault;
using McpServerLibrary.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;

namespace Mcp.AzureFunction;

/// <summary>
/// MCP tools exposed through the Azure Functions MCP extension. The generated
/// endpoints are /runtime/webhooks/mcp (Streamable HTTP) and /runtime/webhooks/mcp/sse.
/// </summary>
public sealed class McpToolFunctions
{
    private readonly IGenericApiClient _api;
    private readonly IKeyVaultCredentialProvider _credentials;
    private readonly ILogger<McpToolFunctions> _logger;

    public McpToolFunctions(
        IGenericApiClient api,
        IKeyVaultCredentialProvider credentials,
        ILogger<McpToolFunctions> logger)
    {
        _api = api;
        _credentials = credentials;
        _logger = logger;
    }

    [Function(nameof(GetStatus))]
    public async Task<string> GetStatus(
        [McpToolTrigger("get_status", "Returns the current status of a resource from the downstream API, e.g. an order or a shipment.")]
            ToolInvocationContext context,
        [McpToolProperty("resourceType", "Resource collection to look in, e.g. \"orders\".", true)]
            string resourceType,
        [McpToolProperty("resourceId", "Identifier of the resource, e.g. \"ORD-000123\".", true)]
            string resourceId)
    {
        _logger.LogInformation("get_status called for {ResourceType}/{ResourceId}", resourceType, resourceId);

        var credentials = await _credentials.GetCredentialsAsync();
        return await _api.GetAsync($"{resourceType}/{resourceId}", credentials);
    }

    [Function(nameof(SendUpdate))]
    public async Task<string> SendUpdate(
        [McpToolTrigger("send_update", "Sends an update to a resource in the downstream API by POSTing a JSON payload.")]
            ToolInvocationContext context,
        [McpToolProperty("resourceType", "Resource collection to update, e.g. \"orders\".", true)]
            string resourceType,
        [McpToolProperty("resourceId", "Identifier of the resource, e.g. \"ORD-000123\".", true)]
            string resourceId,
        [McpToolProperty("jsonBody", "JSON payload to POST to the downstream API.", true)]
            string jsonBody)
    {
        _logger.LogInformation("send_update called for {ResourceType}/{ResourceId}", resourceType, resourceId);

        var credentials = await _credentials.GetCredentialsAsync();
        return await _api.PostAsync($"{resourceType}/{resourceId}", jsonBody, credentials);
    }
}