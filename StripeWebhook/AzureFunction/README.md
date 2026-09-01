# StripeWebhook Azure Function

An isolated-worker Azure Function (.NET 10) that receives
[Stripe webhook events](https://docs.stripe.com/webhooks) - a single
generic endpoint, matching how Stripe itself works: you register **one**
webhook URL in the Stripe Dashboard (or via the API) and choose which
event types get delivered to it, rather than having a different URL per
event type.

## Endpoint

| Method | Route |
|---|---|
| `POST` | `/stripe/webhook` |

Register this URL as a webhook endpoint in the
[Stripe Dashboard](https://dashboard.stripe.com/webhooks) (or via the
[Webhook Endpoints API](https://docs.stripe.com/api/webhook_endpoints)),
select the event types you want delivered, and copy the endpoint's signing
secret into `StripeWebhookSecret` (below).

## What this does and doesn't do

1. Reads the raw request body (unmodified - signature verification needs
   the exact bytes Stripe sent).
2. Verifies the `Stripe-Signature` header against `StripeWebhookSecret`.
   Invalid or missing signature -> HTTP 400, nothing else happens.
3. Parses just the `id` and `type` fields from the (now-trusted) JSON body.
4. Runs the event type through a `switch` grouped by category, **purely to
   log which category the event falls into** - every branch is a no-op
   placeholder. See "Event catalog" below for what's covered.
5. Returns HTTP 200 with `{"received": true, "id": "...", "type": "..."}`.

**This function does not implement any business logic** - no database
writes, no emails, no order creation, nothing tied to any particular
product. Wire your own handling into the relevant `case` (or replace the
switch with your own dispatch) once you know which event types your
application actually needs to act on.

### Why always return 200

Per [Stripe's own guidance](https://docs.stripe.com/webhooks#acknowledge-events-immediately),
an endpoint should acknowledge receipt quickly and do any slow/async work
afterward - a non-2xx response makes Stripe retry the same event on a
backoff schedule. This sample returns 200 for every event type it doesn't
specifically recognize too (see the `default` case), since an unrecognized
type is normal (Stripe adds event types over time) rather than an error.
The only conditions that produce a non-2xx response here are a failed
signature check, a missing/misconfigured webhook secret, or a body that
isn't valid JSON.

## Authentication

This endpoint uses `AuthorizationLevel.Anonymous` - **intentionally, not an
oversight**. Stripe's webhook sender can't attach a custom header or query
parameter like an Azure Functions key, so the `Stripe-Signature` check
described above **is** this endpoint's authentication. Never remove that
check while leaving the endpoint anonymous.

### How signature verification works

Implemented by hand in `Services/StripeSignatureVerifier.cs` using the
BCL's `HMACSHA256` - no Stripe.net SDK dependency, per
[Stripe's publicly documented signing scheme](https://docs.stripe.com/webhooks#verify-manually):

1. The `Stripe-Signature` header looks like `t=1614556800,v1=5257a869e7ec...`
   (Stripe sends multiple `v1` values while a signing secret is being
   rotated - a match against any one is valid).
2. The signed payload is the literal string `"{t}.{raw request body}"`,
   HMAC-SHA256'd with the webhook's signing secret.
3. The computed signature is compared against each provided `v1` value
   using a constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
4. `t` must be within 5 minutes of the current time (replay protection).

## Required environment variables

| Name | Description |
|---|---|
| `StripeWebhookSecret` | The signing secret for this specific webhook endpoint (starts with `whsec_`) - copy it from the Stripe Dashboard when you create or view the endpoint |

See `local.settings.json.sample` for the local `local.settings.json` shape
- copy it and fill in a real value, which stays git-ignored.

## Testing locally

`func start`, then either:

- Use the [Stripe CLI](https://docs.stripe.com/stripe-cli): `stripe listen --forward-to localhost:7071/stripe/webhook`
  forwards real (test-mode) events and prints a temporary webhook secret to
  use locally; `stripe trigger payment_intent.succeeded` fires a sample
  event.
- Use the sample clients in [`../ClientSamples`](../ClientSamples), which
  construct a signed test request themselves (no Stripe CLI or real Stripe
  account needed) using the exact same signing scheme described above.

## Event catalog

Every event type below reaches the same `switch` in
`Functions/StripeWebhookFunction.cs`, grouped by category purely for
logging - none of these trigger any actual behavior in this sample.
Anything not listed still returns 200 via the `default` case (see "Why
always return 200"). This isn't Stripe's complete event list (Stripe has
[hundreds](https://docs.stripe.com/api/events/types) and adds more over
time) - it's the common ones across the object types most integrations
touch.

| Category | Event types |
|---|---|
| Charges | `charge.succeeded`, `charge.failed`, `charge.pending`, `charge.updated`, `charge.captured`, `charge.expired` |
| Refunds | `charge.refunded`, `refund.created`, `refund.updated`, `refund.failed`, `charge.refund.updated` |
| Disputes | `charge.dispute.created`, `charge.dispute.updated`, `charge.dispute.closed`, `charge.dispute.funds_withdrawn`, `charge.dispute.funds_reinstated` |
| PaymentIntents | `payment_intent.created`, `payment_intent.processing`, `payment_intent.requires_action`, `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`, `payment_intent.amount_capturable_updated`, `payment_intent.partially_funded` |
| SetupIntents | `setup_intent.created`, `setup_intent.succeeded`, `setup_intent.setup_failed`, `setup_intent.canceled`, `setup_intent.requires_action` |
| Checkout Sessions | `checkout.session.completed`, `checkout.session.expired`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed` |
| Customers | `customer.created`, `customer.updated`, `customer.deleted` |
| Customer payment sources | `customer.source.created`, `customer.source.updated`, `customer.source.deleted`, `customer.source.expiring` |
| Subscriptions | `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `customer.subscription.paused`, `customer.subscription.resumed`, `customer.subscription.trial_will_end`, `customer.subscription.pending_update_applied`, `customer.subscription.pending_update_expired` |
| Invoices | `invoice.created`, `invoice.finalized`, `invoice.finalization_failed`, `invoice.paid`, `invoice.payment_failed`, `invoice.payment_succeeded`, `invoice.payment_action_required`, `invoice.upcoming`, `invoice.updated`, `invoice.voided`, `invoice.marked_uncollectible`, `invoice.sent` |
| Payment methods | `payment_method.attached`, `payment_method.detached`, `payment_method.updated`, `payment_method.automatically_updated` |
| Payouts | `payout.created`, `payout.updated`, `payout.paid`, `payout.failed`, `payout.canceled` |
| Product/price catalog | `product.created`, `product.updated`, `product.deleted`, `price.created`, `price.updated`, `price.deleted` |

## Design notes

- **No SDK.** No `Stripe.net` dependency - signature verification is ~50
  lines of BCL cryptography, and the event body is read as raw JSON
  (`System.Text.Json`) rather than deserialized into Stripe's object model,
  consistent with the other integrations in this repo staying dependency-light.
- **Self-contained.** This function does not call or depend on any other
  project in this repository.
