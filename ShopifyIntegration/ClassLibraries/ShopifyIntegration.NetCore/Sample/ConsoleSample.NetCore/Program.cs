using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopifyIntegration.NetCore;

var shopDomain = Environment.GetEnvironmentVariable("SHOPIFY_SHOP_DOMAIN") ?? "your-store.myshopify.com";
var accessToken = Environment.GetEnvironmentVariable("SHOPIFY_ACCESS_TOKEN") ?? "REPLACE_WITH_YOUR_ACCESS_TOKEN";

var builder = Host.CreateApplicationBuilder(args);

// AddHttpClient() registers IHttpClientFactory, which manages pooled connections
// for you - the recommended pattern instead of newing up HttpClient yourself in a
// long-running app.
builder.Services.AddHttpClient();
builder.Services.AddSingleton(sp => new ShopifyClient(
    shopDomain, accessToken, httpClient: sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ShopifyClient))));

using var host = builder.Build();

var client = host.Services.GetRequiredService<ShopifyClient>();

// GET (list) - first page of customers.
var list = await client.ListAsync(ShopifyResource.Customers, "limit=5");
Console.WriteLine($"[List customers] HTTP {list.StatusCode}");
Console.WriteLine(list.Body);

// POST (create) - request body is the exact Shopify-shaped envelope.
const string newCustomerJson = """
{
    "customer": {
        "first_name": "Jane",
        "last_name": "Doe",
        "email": "jane.doe@example.com"
    }
}
""";
var created = await client.CreateAsync(ShopifyResource.Customers, newCustomerJson);
Console.WriteLine($"[Create customer] HTTP {created.StatusCode}");
Console.WriteLine(created.Body);

// The rest (GetAsync/UpdateAsync/DeleteAsync) all need a real id from your store -
// shown here as a reference, not run against a live id:
//
// var one = await client.GetAsync(ShopifyResource.Customers, 123456789);
// var updated = await client.UpdateAsync(ShopifyResource.Customers, 123456789,
//     """{ "customer": { "id": 123456789, "last_name": "Smith" } }""");
// var deleted = await client.DeleteAsync(ShopifyResource.Customers, 123456789);
