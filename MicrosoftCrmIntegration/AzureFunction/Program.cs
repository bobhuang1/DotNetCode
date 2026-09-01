#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MicrosoftCrmIntegration.AzureFunction.Services;

namespace MicrosoftCrmIntegration.AzureFunction
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
                    services.AddHttpClient(DataverseProxyClient.HttpClientName, client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(HttpClientTimeoutSeconds);
                        client.DefaultRequestHeaders.Add("User-Agent", "MicrosoftCrmIntegration Azure Function");
                    });

                    services.AddSingleton<DataverseProxyClient>();
                })
                .Build();

            host.Run();
        }
    }
}
