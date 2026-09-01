// Sample client for the ShopifyIntegration Azure Function, written for .NET 6/8/10.
//
// What's different here vs. the .NET Framework 4.8 sample in ../NetFramework48:
//   - HttpClient comes from IHttpClientFactory via the generic host's DI container
//     (AddHttpClient<T>), which is the idiomatic modern pattern: it manages the
//     underlying SocketsHttpHandler pool/lifetime for you, so there's no need for a
//     hand-rolled static singleton HttpClient.
//   - Top-level statements instead of a Main method.
//   - TLS 1.2+ is the platform default, so nothing needs to be set explicitly.
//   - No JSON library is needed on either sample: the Function's request/response
//     bodies are Shopify's own JSON, forwarded as plain strings.

using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                       ?? "https://your-function-app.azurewebsites.net";
var functionKey = Environment.GetEnvironmentVariable("FUNCTION_KEY")
                   ?? "REPLACE_WITH_YOUR_FUNCTION_KEY";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient<ShopifyIntegrationClient>(client =>
{
    client.BaseAddress = new Uri(functionBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
});

using var host = builder.Build();

var client = host.Services.GetRequiredService<ShopifyIntegrationClient>();

await client.ListCustomersAsync();
await client.CreateCustomerAsync();

internal sealed class ShopifyIntegrationClient(HttpClient httpClient)
{
    /// <summary>Calls GET /shopify/customers?limit=5.</summary>
    public async Task ListCustomersAsync()
    {
        using var response = await httpClient.GetAsync("shopify/customers?limit=5");
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[List customers] HTTP {(int)response.StatusCode} - {responseBody}");
    }

    /// <summary>Calls POST /shopify/customers with a Shopify-shaped request body.</summary>
    public async Task CreateCustomerAsync()
    {
        const string json = """
        {
            "customer": {
                "first_name": "Jane",
                "last_name": "Doe",
                "email": "jane.doe@example.com"
            }
        }
        """;

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync("shopify/customers", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[Create customer] HTTP {(int)response.StatusCode} - {responseBody}");
    }
}
