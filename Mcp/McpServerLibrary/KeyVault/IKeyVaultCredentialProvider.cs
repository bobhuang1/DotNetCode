using McpServerLibrary.Configuration;

namespace McpServerLibrary.KeyVault
{
    /// <summary>
    /// Resolves generic credentials and individual secrets from Azure Key Vault.
    /// </summary>
    public interface IKeyVaultCredentialProvider
    {
        /// <summary>
        /// Builds <see cref="GenericCredentials"/> by reading the configured user-name and
        /// API-key secrets from Azure Key Vault.
        /// </summary>
        Task<GenericCredentials> GetCredentialsAsync(CancellationToken cancellationToken = default);

        /// <summary>Reads a single secret from Azure Key Vault.</summary>
        Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default);
    }
}