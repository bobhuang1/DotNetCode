namespace McpServerLibrary.Configuration
{
    /// <summary>
    /// Generic credentials for a downstream REST API. Values are assumed to come from
    /// a secret store (Azure Key Vault), never from source code or configuration files.
    /// </summary>
    public sealed class GenericCredentials
    {
        /// <summary>User name sent with Basic authentication to the downstream API.</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>API key / password sent with Basic authentication to the downstream API.</summary>
        public string ApiSecret { get; set; } = string.Empty;

        /// <summary>Base URL of the downstream API, e.g. https://api.example.com/v1.</summary>
        public string BaseUrl { get; set; } = string.Empty;
    }
}