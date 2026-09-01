# ShopifyIntegration.NetFramework

A .NET Framework 4.8 class library that calls the
[Shopify Admin REST API](https://shopify.dev/docs/api/admin-rest) directly
over HTTP for CRUD operations on the four most common resources -
customers, orders, draft orders (Shopify's closest equivalent to a
standalone "invoice" - see below), and products. No Shopify SDK dependency,
no Shopify-specific object model: request and response bodies are passed
through as raw JSON strings exactly as Shopify's REST API defines them, so
this stays generic instead of reimplementing Shopify's schema.

## Install

Add a project reference (or pack/publish this as a NuGet package yourself)
to `ShopifyIntegration.NetFramework.csproj`.

## Quick start

```csharp
using ShopifyIntegration.NetFramework;

var client = new ShopifyClient(shopDomain: "your-store.myshopify.com", accessToken: "...");

// GET (list)
var list = await client.ListAsync(ShopifyResource.Customers, "limit=50");
Console.WriteLine(list.Body); // raw JSON: { "customers": [ ... ] }

// GET (single)
var one = await client.GetAsync(ShopifyResource.Customers, 123456789);

// POST (create) - body is the exact Shopify-shaped envelope
var created = await client.CreateAsync(ShopifyResource.Customers, @"{
    ""customer"": { ""first_name"": ""Jane"", ""last_name"": ""Doe"", ""email"": ""jane.doe@example.com"" }
}");

// PUT (update) - exposed as UpdateAsync; see "Why PUT, not PATCH" below
var updated = await client.UpdateAsync(ShopifyResource.Customers, 123456789, @"{
    ""customer"": { ""id"": 123456789, ""last_name"": ""Smith"" }
}");

// DELETE
var deleted = await client.DeleteAsync(ShopifyResource.Customers, 123456789);
```

Every call returns a `ShopifyApiResult`:

```csharp
public sealed class ShopifyApiResult
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }     // Shopify's HTTP status, or 0 if the request never reached Shopify
    public string Body { get; set; }        // raw JSON Shopify returned (success or error)
    public string? ErrorMessage { get; set; }
}
```

Deserialize `Body` into whatever model fits your app - this library
intentionally doesn't ship one.

## Supported resources

| `ShopifyResource` | URL segment | Notes |
|---|---|---|
| `Customers` | `customers` | |
| `Orders` | `orders` | |
| `DraftOrders` | `draft_orders` | Shopify's closest equivalent to a standalone "invoice" - build one up, then email it to a customer as a payable invoice via Shopify's `send_invoice` action (not wrapped here; call it directly with `CreateAsync`/a raw request if you need it) |
| `Products` | `products` | |

All four follow the same flat `/admin/api/{version}/{resource}.json` URL
shape. Resources nested under a parent (e.g. an order's fulfillments,
which live at `/orders/{order_id}/fulfillments.json`) are out of scope for
this generic client - add your own method following the same `SendAsync`
pattern if you need one.

## Why PUT, not PATCH

Shopify's REST Admin API updates resources via HTTP `PUT`, not `PATCH` -
there is no PATCH verb in their API. This client's update method is named
`UpdateAsync` (not `PatchAsync`) so the name doesn't imply a verb it
doesn't use, and internally issues a `PUT`.

## Authentication

This client uses a static **Admin API access token** - the credential a
custom app (Shopify admin -> Settings -> Apps and sales channels -> Develop
apps) issues for your own store. Grant it only the resource scopes you
need (e.g. `read_customers`/`write_customers`).

This is deliberately simpler than the full OAuth authorization-code flow
Shopify apps use to be installed by *other* merchants' stores - that flow
is for building a publicly-distributed app, out of scope for this generic,
single-store sample.

## Sharing an `HttpClient`

The constructor accepts an optional `HttpClient`. If your app already keeps
a single, long-lived `HttpClient` around (the recommended pattern on .NET
Framework, to avoid socket exhaustion from creating one per request), pass
it in and the client will reuse it instead of creating its own:

```csharp
var sharedClient = new HttpClient();
var client = new ShopifyClient(shopDomain, accessToken, httpClient: sharedClient);
```

## TLS 1.2

The client sets `ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12`
in a static constructor, since TLS 1.2 is not always the OS/.NET Framework
default on older Windows machines and Shopify's API requires it. You don't
need to set this yourself.

## Rate limiting

Shopify's REST Admin API throttles with HTTP 429 and a `Retry-After`
header. This client retries once, honoring `Retry-After` if present (a
fixed 2-second fallback otherwise); a second 429 is returned to you as a
normal (non-success) `ShopifyApiResult` rather than thrown.

## Running the sample

```
cd Sample\ConsoleSample.NetFramework
set SHOPIFY_SHOP_DOMAIN=your-store.myshopify.com
set SHOPIFY_ACCESS_TOKEN=your-admin-api-access-token
dotnet run
```
