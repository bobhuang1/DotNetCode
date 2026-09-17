using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;
using McpServerLibrary.Configuration;

namespace McpServerLibrary.KeyVault
{
    /// <summary>
    /// <see cref="IKeyVaultCredentialProvider"/> backed by Azure Key Vault,
    /// authenticated with <see cref="DefaultAzureCredential"/>.
    /// </summary>
    /// <remarks>
    /// DefaultAzureCredential works on any host: locally it picks up az login / Visual
    /// Studio / Azure CLI sign-ins, in Azure it uses managed identity. Create the secrets
    /// up front, e.g. <c>az keyvault secret set --name mcp-sample-api-key --value "..."</c>.
    /// </remarks>
    public sealed class KeyVaultCredentialProvider : IKeyVaultCredentialProvider
    {
        private readonly SecretClient _client;
        private readonly KeyVaultSettings _settings;

        public KeyVaultCredentialProvider(IOptions<KeyVaultSettings> settings)
        {
            _settings = settings.Value;

            if (string.IsNullOrWhiteSpace(_settings.VaultUrl))
            {
                throw new InvalidOperationException(
                    "KeyVaultSettings.VaultUrl is not configured. Point it at your Key Vault, e.g. https://my-vault.vault.azure.net/.");
            }

            _client = new SecretClient(new Uri(_settings.VaultUrl), new DefaultAzureCredential());
        }

        public async Task<GenericCredentials> GetCredentialsAsync(CancellationToken cancellationToken = default)
        {
            var userNameTask = GetSecretAsync(_settings.UserNameSecretName, cancellationToken);
            var apiSecretTask = GetSecretAsync(_settings.ApiSecretSecretName, cancellationToken);

            await Task.WhenAll(userNameTask, apiSecretTask).ConfigureAwait(false);

            return new GenericCredentials
            {
                UserName = await userNameTask.ConfigureAwait(false),
                ApiSecret = await apiSecretTask.ConfigureAwait(false),
                BaseUrl = _settings.DownstreamApiBaseUrl,
            };
        }

        public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            var response = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Value.Value ?? string.Empty;
        }
    }
}