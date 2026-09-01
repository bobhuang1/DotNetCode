// Sample client for the MicrosoftCrmIntegration Azure Function, written for .NET 6/8/10.
//
// What's different here vs. the .NET Framework 4.8 sample in ../NetFramework48:
//   - HttpClient comes from IHttpClientFactory via the generic host's DI container
//     (AddHttpClient<T>), which is the idiomatic modern pattern: it manages the
//     underlying SocketsHttpHandler pool/lifetime for you, so there's no need for a
//     hand-rolled static singleton HttpClient.
//   - Top-level statements instead of a Main method.
//   - TLS 1.2+ is the platform default, so nothing needs to be set explicitly.
//   - No JSON library is needed on either sample: the Function's request/response
//     bodies are Dataverse's own JSON, forwarded as plain strings.

using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                       ?? "https://your-function-app.azurewebsites.net";
var functionKey = Environment.GetEnvironmentVariable("FUNCTION_KEY")
                   ?? "REPLACE_WITH_YOUR_FUNCTION_KEY";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient<MicrosoftCrmIntegrationClient>(client =>
{
    client.BaseAddress = new Uri(functionBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
});

using var host = builder.Build();

var client = host.Services.GetRequiredService<MicrosoftCrmIntegrationClient>();

await client.ListAccountsAsync();
await client.CreateContactAsync();

internal sealed class MicrosoftCrmIntegrationClient(HttpClient httpClient)
{
    /// <summary>Calls GET /dataverse/accounts with OData query options.</summary>
    public async Task ListAccountsAsync()
    {
        using var response = await httpClient.GetAsync("dataverse/accounts?$select=name,accountnumber&$top=5");
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[List accounts] HTTP {(int)response.StatusCode} - {responseBody}");
    }

    /// <summary>Calls POST /dataverse/contacts with a Dataverse-shaped request body.</summary>
    public async Task CreateContactAsync()
    {
        const string json = """
        {
            "firstname": "Jane",
            "lastname": "Doe",
            "emailaddress1": "jane.doe@example.com"
        }
        """;

        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync("dataverse/contacts", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[Create contact] HTTP {(int)response.StatusCode} - {responseBody}");
    }
}
