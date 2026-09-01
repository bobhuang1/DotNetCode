#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopifyIntegration.AzureFunction.Services;

namespace ShopifyIntegration.AzureFunction
{
    public class Program
    {
        private const int HttpClientTimeoutSeconds = 30;

        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureServices(services =>
                {
                    services.AddHttpClient(ShopifyProxyClient.HttpClientName, client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(HttpClientTimeoutSeconds);
                        client.DefaultRequestHeaders.Add("User-Agent", "ShopifyIntegration Azure Function");
                    });

                    services.AddSingleton<ShopifyProxyClient>();
                })
                .Build();

            host.Run();
        }
    }
}
