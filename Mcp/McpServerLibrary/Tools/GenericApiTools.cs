using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using McpServerLibrary.Configuration;
using McpServerLibrary.KeyVault;
using McpServerLibrary.Services;

namespace McpServerLibrary.Tools
{
    /// <summary>
    /// Generic MCP tools that call a downstream REST API using credentials resolved
    /// from Azure Key Vault. Consumed identically by the .NET 10 WebApi host
    /// (via <c>MapMcp</c>) and the .NET Framework 4.8 stdio host.
    /// </summary>
    [McpServerToolType]
    public sealed class GenericApiTools
    {
        private readonly IKeyVaultCredentialProvider _credentials;
        private readonly IGenericApiClient _api;
        private readonly KeyVaultSettings _settings;
        private readonly ILogger<GenericApiTools> _logger;

        public GenericApiTools(
            IKeyVaultCredentialProvider credentials,
            IGenericApiClient api,
            IOptions<KeyVaultSettings> settings,
            ILogger<GenericApiTools> logger)
        {
            _credentials = credentials;
            _api = api;
            _settings = settings.Value;
            _logger = logger;
        }

        [McpServerTool]
        [Description("Returns the current status of a resource from the downstream API, e.g. an order or a shipment. The caller passes a resourceType such as \"orders\" and the resource id.")]
        public async Task<string> GetStatus(
            string resourceType,
            string resourceId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("GetStatus called for {ResourceType}/{ResourceId}", resourceType, resourceId);

            var creds = await _credentials.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
            return await _api.GetAsync($"{resourceType}/{resourceId}", creds, cancellationToken).ConfigureAwait(false);
        }

        [McpServerTool]
        [Description("Sends an update to a resource in the downstream API by POSTing a JSON payload. Useful for status transitions such as approving, cancelling, or amending an order.")]
        public async Task<string> SendUpdate(
            string resourceType,
            string resourceId,
            string jsonBody,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("SendUpdate called for {ResourceType}/{ResourceId}", resourceType, resourceId);

            var creds = await _credentials.GetCredentialsAsync(cancellationToken).ConfigureAwait(false);
            return await _api.PostAsync($"{resourceType}/{resourceId}", jsonBody, creds, cancellationToken).ConfigureAwait(false);
        }
    }
}