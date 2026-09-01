#nullable enable
using Microsoft.Extensions.Hosting;

namespace StripeWebhook.AzureFunction
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .Build();

            host.Run();
        }
    }
}
