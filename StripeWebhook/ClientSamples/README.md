# ClientSamples

Stripe webhooks are inbound - Stripe calls *you* - so there's no CRUD API
here to write a normal client for. Instead, both samples simulate what
Stripe itself does: build a JSON event body, sign it with the same
HMAC-SHA256 scheme the [`AzureFunction`](../AzureFunction) verifies
(`Services/StripeSignatureVerifier.cs`), and POST it with a
`Stripe-Signature` header. That lets you test the function end-to-end
without the [Stripe CLI](https://docs.stripe.com/stripe-cli) or a real
Stripe account - as long as `STRIPE_WEBHOOK_SECRET` here matches
`StripeWebhookSecret` in the function's configuration.

Two separate samples are provided because the idiomatic way to create and
use an `HttpClient` differs between the two platforms:

| Sample | Target | `HttpClient` comes from | Hex encoding |
|---|---|---|---|
| [`NetFramework48`](NetFramework48) | .NET Framework 4.7/4.8 | A hand-rolled `static readonly HttpClient`, with TLS 1.2 set explicitly via `ServicePointManager` | Hand-rolled byte-by-byte (`Convert.ToHexString` doesn't exist on net48) |
| [`Net8Plus`](Net8Plus) | .NET 6/8/10 | `IHttpClientFactory` via `AddHttpClient<T>()` on the generic host's DI container | `Convert.ToHexString(...).ToLowerInvariant()` |

Both samples send the same minimal `payment_intent.succeeded`-shaped test
event.

## Running a sample

Start the function locally first (`func start` in `../AzureFunction`),
then:

```
cd NetFramework48 && dotnet run
cd Net8Plus && dotnet run
```

Set these environment variables first (both samples fall back to
`http://localhost:7071` and a placeholder secret otherwise):

```
set FUNCTION_BASE_URL=http://localhost:7071
set STRIPE_WEBHOOK_SECRET=whsec_...   (must match StripeWebhookSecret in the function's local.settings.json)
```

A successful run prints `HTTP 200 - {"received":true,"id":"evt_test_...","type":"payment_intent.succeeded"}`.
