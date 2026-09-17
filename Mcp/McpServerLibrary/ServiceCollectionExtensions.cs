using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using McpServerLibrary.Configuration;
using McpServerLibrary.KeyVault;
using McpServerLibrary.Services;

namespace McpServerLibrary
{
    /// <summary>
    /// Registers Key Vault-backed credentials and the generic API client so any host
    /// (ASP.NET Core, Azure Functions isolated worker, plain console hosts) can share them.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>Registers library services with settings provided programmatically.</summary>
        public static IServiceCollection AddMcpServerLibrary(this IServiceCollection services, Action<KeyVaultSettings> configure)
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configure is null) throw new ArgumentNullException(nameof(configure));

            services.Configure(configure);
            AddCoreServices(services);
            return services;
        }

        /// <summary>Registers library services by binding the given configuration section (default <c>"KeyVault"</c>).</summary>
        public static IServiceCollection AddMcpServerLibrary(this IServiceCollection services, IConfiguration configuration, string sectionName = "KeyVault")
        {
            if (services is null) throw new ArgumentNullException(nameof(services));
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            var entry = configuration.GetSection(sectionName).Get<KeyVaultSettings>() ?? new KeyVaultSettings();
            services.AddMcpServerLibrary(settings =>
            {
                settings.VaultUrl = entry.VaultUrl;
                settings.UserNameSecretName = entry.UserNameSecretName;
                settings.ApiSecretSecretName = entry.ApiSecretSecretName;
                settings.DownstreamApiBaseUrl = entry.DownstreamApiBaseUrl;
                settings.McpClientApiKeySecretName = entry.McpClientApiKeySecretName;
            });

            return services;
        }

        private static void AddCoreServices(IServiceCollection services)
        {
            services.AddSingleton<IKeyVaultCredentialProvider, KeyVaultCredentialProvider>();
            services.AddHttpClient<IGenericApiClient, GenericApiClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
    }
}