using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartyStreetsLookup.NetCore;
using SmartyStreetsLookup.NetCore.Models;

var usAuthId = Environment.GetEnvironmentVariable("SMARTYSTREETS_US_AUTH_ID") ?? "REPLACE_WITH_YOUR_AUTH_ID";
var usAuthToken = Environment.GetEnvironmentVariable("SMARTYSTREETS_US_AUTH_TOKEN") ?? "REPLACE_WITH_YOUR_AUTH_TOKEN";
var intlAuthId = Environment.GetEnvironmentVariable("SMARTYSTREETS_INTL_AUTH_ID") ?? "REPLACE_WITH_YOUR_AUTH_ID";
var intlAuthToken = Environment.GetEnvironmentVariable("SMARTYSTREETS_INTL_AUTH_TOKEN") ?? "REPLACE_WITH_YOUR_AUTH_TOKEN";

var builder = Host.CreateApplicationBuilder(args);

// AddHttpClient() registers IHttpClientFactory, which manages pooled connections
// for you - the recommended pattern instead of newing up HttpClient yourself in a
// long-running app. Each client below gets its own named HttpClient from the factory.
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp => new SmartyStreetsUsAddressClient(
    usAuthId, usAuthToken, sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SmartyStreetsUsAddressClient))));
builder.Services.AddSingleton(sp => new SmartyStreetsInternationalAddressClient(
    intlAuthId, intlAuthToken, sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SmartyStreetsInternationalAddressClient))));

using var host = builder.Build();

var usClient = host.Services.GetRequiredService<SmartyStreetsUsAddressClient>();
var usResult = await usClient.LookupAsync(new AddressLookupRequest
{
    CompanyName = "Acme Corp",
    AddressLine1 = "1600 Amphitheatre Pkwy",
    City = "Mountain View",
    State = "CA",
    PostalCode = "94043"
});
Console.WriteLine($"[US] IsValid={usResult.IsValid} Message={usResult.Message}");
Console.WriteLine($"[US] {usResult.AddressLine1}, {usResult.AddressLine2}, {usResult.City}, {usResult.State} {usResult.PostalCode}");

var intlClient = host.Services.GetRequiredService<SmartyStreetsInternationalAddressClient>();
var intlResult = await intlClient.LookupAsync(new AddressLookupRequest
{
    CompanyName = "Royal Mail",
    AddressLine1 = "100 Victoria Embankment",
    City = "London",
    PostalCode = "EC4Y 0HQ",
    Country = "GB"
});
Console.WriteLine($"[International] IsValid={intlResult.IsValid} Message={intlResult.Message}");
Console.WriteLine($"[International] {intlResult.AddressLine1}, {intlResult.City}, {intlResult.PostalCode} {intlResult.Country}");
