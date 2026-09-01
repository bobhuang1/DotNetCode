# StripeWebhook

A generic [Stripe webhook](https://docs.stripe.com/webhooks) receiver: one
endpoint, signature verification per Stripe's public signing scheme, and a
no-op dispatch grouped by event category covering a broad catalog of
common Stripe events - see [`AzureFunction/README.md`](AzureFunction/README.md)
for the full list.

Unlike the other integration folders in this repo (`ShopifyIntegration`,
`SmartyStreetsLookup`, `MicrosoftCrmIntegration`), this one is a
**receiver**, not a caller - Stripe POSTs to you, you don't call Stripe -
so there are only two pieces here, not three:

| Folder | What it is |
|---|---|
| [`AzureFunction/`](AzureFunction) | An isolated-worker Azure Function (.NET 10) exposing the single generic `POST /stripe/webhook` endpoint |
| [`ClientSamples/`](ClientSamples) | Sample code (.NET Framework 4.7/4.8 and .NET 6/8/10) that builds and signs a test event, to exercise the function without the Stripe CLI or a real Stripe account |

## No SDK

No `Stripe.net` dependency anywhere in this folder. Signature verification
is implemented by hand with the BCL's `HMACSHA256`, following
[Stripe's publicly documented signing scheme](https://docs.stripe.com/webhooks#verify-manually) -
the same approach as this repo's other integrations avoiding vendor SDKs
in favor of plain HTTP/crypto.

## Generic by design

The function does not implement any business logic - no database writes,
no emails, nothing tied to a specific product or workflow. It verifies the
signature, logs which category the event falls into, and acknowledges with
HTTP 200. Wire your own handling into the relevant case once you know
which event types your application needs to act on.

## Authentication

Stripe's webhook sender can't attach a custom auth header or query
parameter the way a normal API client can, so this endpoint is
`AuthorizationLevel.Anonymous` - the `Stripe-Signature` verification
described above **is** the authentication. See
[`AzureFunction/README.md`](AzureFunction/README.md) for details.

## Security note

This is sample/portfolio code. The webhook secret in this repo is a
placeholder (`whsec_...`) - replace it with your own before running
anything for real, and never commit real secrets. `local.settings.json` is
git-ignored for exactly that reason; only `local.settings.json.sample` is
checked in.
