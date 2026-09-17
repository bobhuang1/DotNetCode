# DotNetCode

A collection of sample .NET libraries and Azure Functions, covering common
integration and infrastructure patterns: resilient data access, input
validation, identity/context helpers, diagnostics, and generic CRUD/webhook
integrations with third-party platforms (Microsoft Graph, SmartyStreets,
Shopify, Microsoft Dataverse, Stripe).

Every library here is multi-targeted for **.NET Framework 4.8** and
**.NET 6 / 8 / 10** from a single codebase, and ships with runnable console
samples for both.

## Contents

| Project | What it is |
|---|---|
| [ResilientSqlAccess](ResilientSqlAccess) | SQL Server / Azure SQL data-access client with configurable retry-with-backoff (constant/linear/exponential) for transient failures, built on Polly |
| [CleanValidation](CleanValidation) | Null-tolerant text/number/date cleaning, validation, and formatting helpers for input that sits between raw user/external data and business logic |
| [IdentityContext](IdentityContext) | Windows/domain identity helpers, plus current-web-request user/IP/base-URL access via a small seam interface that works on both classic ASP.NET and ASP.NET Core |
| [AppDiagnostics](AppDiagnostics) | Static facade over `Debug` output / an optional `ILogger`, with exception-formatting and JS-string-escaping helpers |
| [SendingEmailViaMicrosoftGraph](SendingEmailViaMicrosoftGraph) | Sending email via Microsoft Graph instead of SMTP: an Azure Function relay, client samples for it, and standalone class libraries for calling Graph directly |
| [SmartyStreetsLookup](SmartyStreetsLookup) | US/international address validation via SmartyStreets: an Azure Function relay, client samples for it, and standalone class libraries for calling SmartyStreets directly |
| [ShopifyIntegration](ShopifyIntegration) | Generic CRUD (customers, orders, draft orders, products) against the Shopify Admin REST API: an Azure Function relay, client samples for it, and standalone class libraries for calling Shopify directly |
| [MicrosoftCrmIntegration](MicrosoftCrmIntegration) | Generic CRUD against the Microsoft Dataverse Web API (any table - accounts, contacts, incidents, products, price lists): an Azure Function relay, client samples for it, and standalone class libraries for calling Dataverse directly |
| [StripeWebhook](StripeWebhook) | Generic Stripe webhook receiver: signature verification (hand-rolled HMAC-SHA256, no SDK) and a no-op dispatch covering a broad catalog of common Stripe events, plus client samples that build and sign test events |
| [Mcp](Mcp) | Model Context Protocol (MCP) tools exposed through three hosts - a .NET 10 WebApi (Streamable HTTP), a .NET 10 Azure Function (MCP extension), and a .NET Framework 4.8 stdio console host - all sharing one multi-targeted library with Azure Key Vault-backed credentials |
| [ReactSpa](ReactSpa) | Single-repository single-page application: an ASP.NET Core 10 minimal API served with a Vite + React + TypeScript front end (dev proxy + publish-to-wwwroot MSBuild wiring) |
| [BlazorSignalR](BlazorSignalR) | .NET 10 Blazor Web App (interactive server) with an explicit SignalR hub, a hosted telemetry broadcaster, and a realtime page that shows server-pushed updates and user broadcasts |
| [TerraformAzureSite](TerraformAzureSite) | Terraform for a production-shaped Azure site: Front Door Premium + WAF, autoscaling Premium v3 App Service, geo-replicated Azure SQL, Redis, Key Vault, Application Insights, and private endpoints |
| [UnifiedShopping](UnifiedShopping) | Full-stack e-commerce sample ("Silk & Loom" OEM scarves): web storefront + admin sub-site + MAUI iOS/Android app on one ASP.NET Core API, EF Core, Stripe/PayPal seams, UPS/FedEx/USPS/custom carrier integrations, coupon generation, and Azure CI/CD |

## Common threads

- **Multi-targeted, not framework-specific.** `ResilientSqlAccess`,
  `CleanValidation`, `IdentityContext`, and `AppDiagnostics` are each one
  project/one codebase targeting `net48;net6.0;net8.0;net10.0` at once -
  everything in them only needs dependencies available on every target.
  `SendingEmailViaMicrosoftGraph`, `SmartyStreetsLookup`,
  `ShopifyIntegration`, and `MicrosoftCrmIntegration` are the exception:
  their Azure Functions are .NET 10-only (the isolated worker model), so
  each ships separate class libraries for .NET Framework vs. .NET 6+
  callers instead.
- **Not everything targets `net6.0`.** `Mcp` is multi-targeted for `net48`,
  `net8.0`, and `net10.0` only, because the MCP SDK 2.x dependency chain
  requires .NET 8 or later (its `netstandard2.0` asset is what the .NET
  Framework 4.8 stdio host consumes).
- **Apps and infrastructure, not just libraries.** `ReactSpa`,
  `BlazorSignalR`, and the various `*Integration` Azure Functions run as
  full apps; `TerraformAzureSite` is declarative infrastructure with no .NET
  code. Each project's README shows the run/deploy path (the React sample
  also needs Node/npm for its front end).
- **Graceful degradation over exceptions**, where that fits the problem.
  `CleanValidation`'s parsing helpers return a sensible default (`0`,
  `string.Empty`, `DateTime.MinValue`) instead of throwing, and
  `ResilientSqlAccess` returns a `SqlResult` with `Succeeded`/`ErrorMessage`
  instead of throwing on a SQL failure - so callers check a flag instead of
  wrapping every call in try/catch.
- **Configuration over hardcoding.** Organization-specific values (a
  domain-to-email mapping, a structured-code format, retryable SQL error
  codes) are exposed as a parameter, delegate, or dictionary the caller
  populates, rather than baked into the library - see each project's README
  for specifics.
- **Every sample runs standalone.** `cd Samples/NetFramework48 && dotnet run`
  or `cd Samples/Net8Plus && dotnet run` inside each project folder - no
  shared solution-wide setup required.

## Security note

All connection strings, credentials, tenant IDs, domain names, and similar
values across this folder are placeholders - replace them with your own
before running anything for real, and never commit real secrets.
