// Sample client for the ShopifyIntegration Azure Function, written for
// .NET Framework 4.7/4.8.
//
// What's different here vs. the .NET 6/8/10 sample in ../Net8Plus:
//   - No Microsoft.Extensions.Http / IHttpClientFactory. Framework apps typically
//     just new up a single, long-lived, static HttpClient and reuse it for the life
//     of the process (creating a new HttpClient per request risks socket exhaustion
//     under load - the classic footgun on this platform).
//   - TLS 1.2 is not always the OS default on older Windows/.NET Framework
//     combinations, so it's set explicitly via ServicePointManager.
//   - Program entry point is a classic static class with a Main method, rather than
//     top-level statements (top-level statements require C# 9 / .NET 5+ tooling
//     conventions typically paired with newer TFMs).
//   - No JSON library is needed on either sample: the Function's request/response
//     bodies are Shopify's own JSON, forwarded as plain strings.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace ShopifyIntegrationClientSample.NetFramework48
{
    internal static class Program
    {
        // A single static HttpClient shared for the lifetime of the app.
        private static readonly HttpClient Client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static async Task<int> Main()
        {
            // Ensure TLS 1.2 is used regardless of OS/.NET Framework defaults.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                                   ?? "https://your-function-app.azurewebsites.net";
            var functionKey = Environment.GetEnvironmentVariable("FUNCTION_KEY")
                               ?? "REPLACE_WITH_YOUR_FUNCTION_KEY";

            Client.DefaultRequestHeaders.Accept.Clear();
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            Client.DefaultRequestHeaders.Remove("x-functions-key");
            Client.DefaultRequestHeaders.Add("x-functions-key", functionKey);

            try
            {
                await ListCustomersAsync(functionBaseUrl);
                await CreateCustomerAsync(functionBaseUrl);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Request failed: " + ex);
                return 1;
            }
        }

        /// <summary>Calls GET /shopify/customers?limit=5.</summary>
        private static async Task ListCustomersAsync(string baseUrl)
        {
            using (var response = await Client.GetAsync(baseUrl.TrimEnd('/') + "/shopify/customers?limit=5"))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("[List customers] HTTP {0} - {1}", (int)response.StatusCode, responseBody);
            }
        }

        /// <summary>Calls POST /shopify/customers with a Shopify-shaped request body.</summary>
        private static async Task CreateCustomerAsync(string baseUrl)
        {
            const string json = @"{
                ""customer"": {
                    ""first_name"": ""Jane"",
                    ""last_name"": ""Doe"",
                    ""email"": ""jane.doe@example.com""
                }
            }";

            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await Client.PostAsync(baseUrl.TrimEnd('/') + "/shopify/customers", content))
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("[Create customer] HTTP {0} - {1}", (int)response.StatusCode, responseBody);
            }
        }
    }
}
