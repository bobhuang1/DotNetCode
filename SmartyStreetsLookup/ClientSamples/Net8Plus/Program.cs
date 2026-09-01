// Sample client for the SmartyStreetsLookup Azure Function, written for .NET 6/8/10.
//
// What's different here vs. the .NET Framework 4.8 sample in ../NetFramework48:
//   - HttpClient comes from IHttpClientFactory via the generic host's DI container
//     (AddHttpClient<T>), which is the idiomatic modern pattern: it manages the
//     underlying SocketsHttpHandler pool/lifetime for you, so there's no need for a
//     hand-rolled static singleton HttpClient.
//   - System.Text.Json instead of Newtonsoft.Json.
//   - Top-level statements instead of a Main method.
//   - TLS 1.2+ is the platform default, so nothing needs to be set explicitly.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var functionBaseUrl = Environment.GetEnvironmentVariable("FUNCTION_BASE_URL")
                       ?? "https://your-function-app.azurewebsites.net";
var functionKey = Environment.GetEnvironmentVariable("FUNCTION_KEY")
                   ?? "REPLACE_WITH_YOUR_FUNCTION_KEY";

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient<SmartyStreetsLookupClient>(client =>
{
    client.BaseAddress = new Uri(functionBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
});

using var host = builder.Build();

var lookupClient = host.Services.GetRequiredService<SmartyStreetsLookupClient>();

await lookupClient.LookUpUsAddressAsync();
await lookupClient.LookUpInternationalAddressAsync();

internal sealed class SmartyStreetsLookupClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Calls POST /UsAddressLookup with a US address.</summary>
    public async Task LookUpUsAddressAsync()
    {
        var payload = new AddressLookupRequest(
            CompanyName: "Acme Corp",
            AddressLine1: "1600 Amphitheatre Pkwy",
            AddressLine2: "",
            City: "Mountain View",
            State: "CA",
            PostalCode: "94043",
            Country: "");

        using var response = await httpClient.PostAsJsonAsync("UsAddressLookup", payload, JsonOptions);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[US] HTTP {(int)response.StatusCode} - {responseBody}");
    }

    /// <summary>Calls POST /InternationalAddressLookup with a non-US address.</summary>
    public async Task LookUpInternationalAddressAsync()
    {
        var payload = new AddressLookupRequest(
            CompanyName: "Royal Mail",
            AddressLine1: "100 Victoria Embankment",
            AddressLine2: "",
            City: "London",
            State: "",
            PostalCode: "EC4Y 0HQ",
            Country: "GB");

        using var response = await httpClient.PostAsJsonAsync("InternationalAddressLookup", payload, JsonOptions);
        var responseBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[International] HTTP {(int)response.StatusCode} - {responseBody}");
    }
}

internal sealed record AddressLookupRequest(
    string CompanyName,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Country);
