# UnifiedShopping

A generic e-commerce sample in .NET/C#: an OEM scarf store ("Silk & Loom") with a
**customer web storefront**, a **separate admin sub-site**, and an **iOS/Android MAUI
app** - all sharing one API, one database, and one set of domain logic. Designed to be
scaled out on Azure and deployed through the included CI/CD pipelines.

## Project layout

| Project | What it is | Notes |
|---|---|---|
| [`src/ShopCommon/`](src/ShopCommon) | Entities, enums, DTOs, pricing math, coupon-code generator | Pure code with zero dependencies - referenced by every other project |
| [`src/ShopData/`](src/ShopData) | EF Core 8 `ShopDbContext`, migrations, demo seed | SQLite for local dev, Azure SQL for production (`Database:Provider`) |
| [`src/ShopServices/`](src/ShopServices) | Cart, checkout, shipping, admin services; payment gateway and carrier seams | All business rules live here, so every front end gets them for free |
| [`src/ShopApi/`](src/ShopApi) | ASP.NET Core minimal API: storefront endpoints + `/api/admin/*` + webhooks | The single backend both web and mobile talk to |
| [`src/ShopWeb/`](src/ShopWeb) | Razor Pages site: storefront + **admin sub-site** (order queue, coupons, carriers) | Session-based cart; talks to ShopApi over typed `HttpClient` |
| [`src/ShopMobile/`](src/ShopMobile) | MAUI app for iOS/Android: browse, cart, checkout, order status | Same endpoints as the web; see demo-mode note below |
| [`tests/ShopCommon.Tests/`](tests/ShopCommon.Tests) | Unit + database-backed service tests (xUnit) | `dotnet test` in this folder runs 18 tests |
| [`CiCd/`](CiCd) | GitHub Actions, Azure DevOps pipelines, Bicep infrastructure | Build -> test -> migrate -> staging -> smoke -> swap |

## Why "unified"

- **One codebase, three front ends.** Web, admin, and mobile all consume the same
  `ShopCommon` DTOs. Cart totals are computed by the same `PricingCalculator` whether
  the customer is on a laptop or a phone.
- **Shared services, not shared UI.** Business logic (inventory reservation, coupon
  redemption, refund orchestration) lives in `ShopServices`; front ends only render.
- **One database.** EF Core migrations created from the shared `ShopDbContext`;
  SQL row-level concurrency keeps stock counts honest under parallel checkouts.

## Features by area

### Storefront (web + mobile)
- Product catalog with variants (color/size), live stock counts, sale pricing.
- Cart with server-side re-pricing: totals are always recalculated from the database,
  never trusted from the client.
- Checkout: address -> live carrier rates -> **Stripe** (credit/debit) or **PayPal**.
- Order status page with shipment tracking and return status.
- Common pages: shipping policy, privacy policy, terms & conditions.

### Admin sub-site (`/Admin/*` in ShopWeb + `/api/admin/*`)
- **Order queue** with status filter and paged listing; orders appear immediately
  after payment.
- **Fulfillment:** buy shipping labels (UPS / FedEx / USPS / custom), print the
  returned PDF, mark shipped, refresh tracking - all flipping the order timeline.
- **Returns / lost items / insurance claims:** approve return (refund or replace),
  complete it (gateway refund + optional restock), file a carrier claim reference.
- **Coupons:** generate 16-character alphanumeric codes (cryptographically random,
  ambiguous characters excluded) with a percentage discount, optional prefix,
  expiry, usage cap, and minimum subtotal.
- **Carriers:** configure the three built-in carriers or add any number of custom
  carriers by pointing them at a REST endpoint implementing quote/label/track.

### Payments
- `IPaymentGateway` seam with **Stripe** and **PayPal** integration points; the
  sample ships deterministic demo gateways so checkout works offline.
- Webhook endpoints (`/api/webhooks/stripe`, `/api/webhooks/paypal`) flip orders to
  `Paid` - in production verify signatures first (see the repo's `StripeWebhook`
  folder for a hand-rolled HMAC verifier).
- Refunds go through the same gateway seam when a return completes.

### Shipping
- `ICarrierClient` seam per carrier. Mock clients ship in the sample; swap in real
  UPS/FedEx/USPS API implementations without touching UI code.
- `CustomCarrierClient` calls any REST endpoint answering the shared JSON contract -
  new carriers are configuration, not forks.

## High-traffic design notes

The catalog is scarves; the throughput ambitions are not. Choices made for scale:

1. **No overselling under concurrency.** Checkout reserves inventory inside a
   transaction using an optimistic-concurrency token (`LastAdjustedUtc`). Two
   simultaneous buyers of the last scarf produce one winner and one retry.
2. **Stateless web/api tiers.** Session + cart state are cookie/provider-based, so
   App Service can autoscale to N instances. Swap in Redis for distributed cache
   when you do.
3. **Cache the hot path.** Product listings are cache-tagged (`catalog`) with a
   30-second TTL via `AddOutputCache`; eviction is by tag when an admin edits.
4. **Server-side pricing.** The API never trusts client totals - protection both
   against bugs and manipulated checkouts.
5. **Failure isolation.** Carrier rate quoting fans out in parallel and a slow or
   dead carrier is skipped rather than failing checkout.
6. **Async all the way.** Every I/O path is async with cancellation tokens.
7. **Azure-shaped.** `EnableRetryOnFailure` for SQL transient faults, `/health`
   endpoints for probes, App Insights wiring in the Bicep, staging slots for
   zero-downtime swaps. For the full multi-region shape (Front Door + WAF,
   Business Critical SQL with geo-failover, Redis, private endpoints) pair the
   [`CiCd/infra`](CiCd) Bicep with this repo's [`TerraformAzureSite`](../TerraformAzureSite).

## Run it locally

```powershell
# 1. API (SQLite + demo seed on first start, Swagger UI open)
dotnet run --project UnifiedShopping/src/ShopApi
# -> http://localhost:5080/swagger

# 2. Web site (storefront + admin)
dotnet run --project UnifiedShopping/src/ShopWeb
# -> http://localhost:5081  (storefront)
#    http://localhost:5081/Admin/Index (admin; passphrase from appsettings "Admin:Passphrase")

# 3. Mobile (with MAUI workloads installed)
dotnet build UnifiedShopping/src/ShopMobile/ShopMobile.csproj -t:Run -f net10.0-android
```

Try the full loop:

1. Add scarves to the cart on the web site, apply coupon `WELCOME16SHOP` (15% off),
   check out with Stripe or PayPal - the order shows in the admin queue immediately.
2. In the admin, buy a shipping label for the order, download the PDF, mark shipped,
   refresh tracking.
3. Look the order up on `/OrderStatus` (web) or the mobile app's "My Order" tab.

## Demo mode: MAUI without MAUI workloads

The MAUI app targets `net10.0-android`/`net10.0-ios` when MAUI workloads are
installed. To keep the *whole solution* buildable on machines without them, set
`ShopMobileDemoMode=true` - the project compiles as plain `net8.0` with view models
and API client only (`dotnet build UnifiedShopping.slnx -p:ShopMobileDemoMode=true`).
Platform-gated code sits behind `#if MAUI`.

## Tests

```powershell
dotnet test UnifiedShopping/tests/ShopCommon.Tests/ShopCommon.Tests.csproj
```

Covers coupon-code generation rules (length, alphabet, determinism hooks),
pricing math (discounts, tax-after-discount, free-shipping threshold), and
cart/coupon services against a real seeded SQLite database.

## CI/CD

See [`CiCd/README.md`](CiCd/README.md) for the full story: GitHub Actions and Azure
DevOps pipelines both follow build -> test -> **EF migration bundle** -> staging
deploy -> smoke test (`/health`) -> **atomic slot swap**. The Bicep template creates
two App Services (with staging slots), Azure SQL, Key Vault, and App Insights.

## Security note

This is sample/portfolio code. The admin gate is a demo header (`X-Admin-Key`), the
payment gateways are demo stubs, and webhook signature verification is intentionally
omitted. Replace all of them (plus the Stripe/PayPal/SQL credentials) before running
anything real - never commit actual secrets; use Key Vault references in production.
