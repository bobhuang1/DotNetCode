# ShopifyIntegration

Generic sample code for CRUD (`GET`/`POST`/`PATCH`/`DELETE`) operations
against the [Shopify Admin REST API](https://shopify.dev/docs/api/admin-rest)'s
most common resources: customers, orders, draft orders (Shopify's closest
equivalent to a standalone "invoice"), and products.

This folder has three independent, self-contained pieces. Pick whichever
fits your scenario - they don't depend on each other or on any shared
project.

| Folder | What it is | When to use it |
|---|---|---|
| [`AzureFunction/`](AzureFunction) | An isolated-worker Azure Function (.NET 10) exposing generic HTTP CRUD endpoints that proxy to Shopify | You want a shared, centrally-deployed integration point that other apps call over HTTPS without each needing its own Shopify credentials |
| [`ClientSamples/`](ClientSamples) | Sample HTTP client code that calls the Azure Function above | You're integrating a caller with the Azure Function and want a starting point for either .NET Framework 4.7/4.8 or .NET 6/8/10 |
| [`ClassLibraries/`](ClassLibraries) | Two standalone class libraries (`ShopifyIntegration.NetFramework` and `ShopifyIntegration.NetCore`) that call Shopify directly | You want to call Shopify from *inside* your own app, with no extra HTTP hop |

## Generic by design

Every entry point (the Function's endpoints, and both class libraries)
works the same way: request and response bodies are Shopify's own JSON,
passed through unmodified in both directions. Nothing here models
Shopify's schema, wraps its objects, or adds business logic on top - it's a
thin, resource-agnostic CRUD layer over four of Shopify's most common
resources:

| Resource | URL segment |
|---|---|
| Customers | `customers` |
| Orders | `orders` |
| Draft orders (Shopify's "invoice") | `draft_orders` |
| Products | `products` |

All four follow Shopify's flat `/admin/api/{version}/{resource}.json` URL
shape. Resources nested under a parent (e.g. an order's fulfillments) are
out of scope everywhere in this folder - see each piece's README for how
to extend it if you need one.

## Why PUT for updates, exposed as PATCH

Shopify's REST Admin API updates resources via HTTP `PUT` - it has no
PATCH verb. The Azure Function's endpoint accepts `PATCH` (the conventional
REST verb for "update") and translates it to `PUT` before calling Shopify;
the class libraries expose an `UpdateAsync` method (not `PatchAsync`) that
does the same translation, named so it doesn't imply an HTTP verb it
doesn't use.

## Authentication

All three pieces authenticate to Shopify with a static **Admin API access
token** - the credential a custom app (Shopify admin -> Settings -> Apps
and sales channels -> Develop apps) issues for your own store. This is
simpler than the OAuth authorization-code flow Shopify apps use to be
installed by *other* merchants' stores, which is for building a
publicly-distributed app and out of scope here.

## Security note

This is sample/portfolio code. All credentials in this repo are placeholders
(`your-store.myshopify.com`, `REPLACE_WITH_YOUR_ACCESS_TOKEN`, etc.) -
replace them with your own before running anything for real, and never
commit real secrets. `local.settings.json` is git-ignored for exactly that
reason; only `local.settings.json.sample` is checked in.
