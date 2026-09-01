#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartyStreetsLookup.AzureFunction.Services;

namespace SmartyStreetsLookup.AzureFunction
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
                    services.AddHttpClient(SmartyStreetsApiClient.HttpClientName, client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(HttpClientTimeoutSeconds);
                        client.DefaultRequestHeaders.Add("User-Agent", "SmartyStreetsLookup Azure Function");
                    });

                    services.AddSingleton<SmartyStreetsApiClient>();
                })
                .Build();

            host.Run();
        }
    }
}
