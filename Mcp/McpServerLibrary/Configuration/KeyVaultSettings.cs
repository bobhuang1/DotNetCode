namespace McpServerLibrary.Configuration
{
    /// <summary>
    /// Settings that tell the library where Azure Key Vault is and which secret names to read.
    /// </summary>
    public sealed class KeyVaultSettings
    {
        /// <summary>Azure Key Vault URI, e.g. https://&lt;vault-name&gt;.vault.azure.net/.</summary>
        public string VaultUrl { get; set; } = string.Empty;

        /// <summary>Secret name that holds the downstream API user name.</summary>
        public string UserNameSecretName { get; set; } = "mcp-sample-user";

        /// <summary>Secret name that holds the downstream API key / password.</summary>
        public string ApiSecretSecretName { get; set; } = "mcp-sample-api-key";

        /// <summary>Base URL of the generic downstream REST API the tools call.</summary>
        public string DownstreamApiBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Secret name holding the API key that WebApi clients must send as the
        /// <c>x-api-key</c> header to reach the MCP endpoint.
        /// </summary>
        public string McpClientApiKeySecretName { get; set; } = "mcp-client-api-key";
    }
}