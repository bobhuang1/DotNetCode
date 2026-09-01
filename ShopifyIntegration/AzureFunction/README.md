# ShopifyIntegration Azure Function

An isolated-worker Azure Function (.NET 10) exposing generic HTTP endpoints
for CRUD operations against the [Shopify Admin REST API](https://shopify.dev/docs/api/admin-rest)'s
most common resources - customers, orders, draft orders (Shopify's closest
equivalent to a standalone "invoice"), and products. This is a thin proxy:
it does not model Shopify's schema itself, and request/response bodies are
Shopify's own JSON, passed through unmodified in both directions.

## Endpoints

| Method | Route | Shopify call |
|---|---|---|
| `GET` | `/shopify/{resource}` | List (query string forwarded as-is, e.g. `?limit=50&status=any`) |
| `GET` | `/shopify/{resource}/{id}` | Get one by id |
| `POST` | `/shopify/{resource}` | Create |
| `PATCH` | `/shopify/{resource}/{id}` | Update (sent to Shopify as `PUT` - see below) |
| `DELETE` | `/shopify/{resource}/{id}` | Delete |

`{resource}` is one of: `customers`, `orders`, `draft_orders`, `products`.
Any other value returns HTTP 400. All four follow the same flat
`/admin/api/{version}/{resource}.json` shape on Shopify's side; resources
nested under a parent (e.g. an order's fulfillments) aren't supported by
this generic proxy.

### Why PATCH here calls PUT on Shopify

Shopify's REST Admin API has no PATCH verb - it updates resources via
`PUT`. `PATCH` is exposed on this Function's own endpoint because it's the
conventional HTTP verb for "update," and `ShopifyProxyClient` translates it
to `PUT` before calling Shopify.

## Request/response bodies

Bodies are Shopify's exact JSON shape - this proxy does not wrap, unwrap,
or validate them beyond forwarding. For `POST`/`PATCH`, send the full
Shopify-shaped envelope:

```json
{ "customer": { "first_name": "Jane", "last_name": "Doe", "email": "jane.doe@example.com" } }
```

The response is Shopify's own JSON body and HTTP status code, passed
straight through - including Shopify's own 4xx/5xx errors (e.g. a 422 with
field-level validation messages), so a non-2xx response from this endpoint
usually means Shopify itself rejected the request, not this proxy.

This proxy only produces its own error body (`{"error": "..."}`) for three
cases: an unrecognized `{resource}` (400), missing server-side credentials
(500), or a network-level failure reaching Shopify at all (502).

## Authentication

Two layers:

1. **This Function** - an Azure Function key, same as any
   `AuthorizationLevel.Function` HTTP trigger (`?code=...` or
   `x-functions-key` header).
2. **Shopify** - a static Admin API access token from a custom app
   (Shopify admin -> Settings -> Apps and sales channels -> Develop apps),
   configured server-side via environment variables (never sent by the
   caller). This is simpler than Shopify's OAuth authorization-code flow,
   which is for apps installed by *other* merchants - out of scope for this
   single-store proxy.

## Required environment variables

| Name | Description |
|---|---|
| `ShopifyShopDomain` | e.g. `your-store.myshopify.com` |
| `ShopifyAccessToken` | Admin API access token from your custom app |
| `ShopifyApiVersion` | Optional; e.g. `2025-01`. Defaults to a recent stable version if unset - Shopify versions release quarterly, so set this to whichever version you've tested against |

See `local.settings.json.sample` for the local `local.settings.json` shape
- copy it and fill in real values, which stays git-ignored.

## Running locally

```
func start
```

```
curl -X POST http://localhost:7071/shopify/customers ^
  -H "Content-Type: application/json" ^
  -d "{\"customer\":{\"first_name\":\"Jane\",\"last_name\":\"Doe\",\"email\":\"jane.doe@example.com\"}}"

curl http://localhost:7071/shopify/customers?limit=5
curl http://localhost:7071/shopify/customers/123456789
curl -X PATCH http://localhost:7071/shopify/customers/123456789 ^
  -H "Content-Type: application/json" ^
  -d "{\"customer\":{\"id\":123456789,\"last_name\":\"Smith\"}}"
curl -X DELETE http://localhost:7071/shopify/customers/123456789
```

## Design notes

- **No SDK.** Every call goes straight to Shopify over `HttpClient` - no
  ShopifySharp or other Shopify SDK dependency, and no JSON parsing library
  either, since bodies are forwarded as raw strings in both directions.
- **One retry on HTTP 429** (Shopify's REST rate limit), honoring
  `Retry-After` if present, then a fixed 2-second fallback. A failure to
  reach Shopify at all (DNS, timeout, etc.) is reported as HTTP 502 rather
  than thrown.
- **Self-contained.** This function does not call or depend on any other
  project in this repository (no shared library reference) - see
  [`../ClassLibraries`](../ClassLibraries) if you want to call Shopify
  directly from your own app instead of through this relay.
