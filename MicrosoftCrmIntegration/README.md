# MicrosoftCrmIntegration

Generic sample code for CRUD (`GET`/`POST`/`PATCH`/`DELETE`) operations
against the [Microsoft Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- the REST/OData API behind Dynamics 365 CE / Power Platform's "Microsoft
CRM" - against any table: customers (`accounts`/`contacts`), products,
price lists (`pricelevels`), support cases (`incidents`), or a custom one.

This folder has three independent, self-contained pieces. Pick whichever
fits your scenario - they don't depend on each other or on any shared
project.

| Folder | What it is | When to use it |
|---|---|---|
| [`AzureFunction/`](AzureFunction) | An isolated-worker Azure Function (.NET 10) exposing generic HTTP CRUD endpoints that proxy to Dataverse | You want a shared, centrally-deployed integration point that other apps call over HTTPS without each needing its own Dataverse credentials |
| [`ClientSamples/`](ClientSamples) | Sample HTTP client code that calls the Azure Function above | You're integrating a caller with the Azure Function and want a starting point for either .NET Framework 4.7/4.8 or .NET 6/8/10 |
| [`ClassLibraries/`](ClassLibraries) | Two standalone class libraries (`MicrosoftCrmIntegration.NetFramework` and `MicrosoftCrmIntegration.NetCore`) that call Dataverse directly | You want to call Dataverse from *inside* your own app, with no extra HTTP hop |

## Generic by design

Every entry point (the Function's endpoints, and both class libraries)
works the same way: request and response bodies are Dataverse's own JSON,
passed through unmodified in both directions, and **any** table name is
accepted - standard (`accounts`, `contacts`, `incidents`, `products`,
`pricelevels`, ...) or custom. Nothing here models Dataverse's schema,
wraps its records, or adds business logic on top.

| Concept | Entity set name |
|---|---|
| Customer (company) | `accounts` |
| Customer (person) | `contacts` |
| Support case | `incidents` (Dataverse's "Case" entity is internally named `incident`) |
| Product | `products` |
| Price list | `pricelevels` |

Unlike the [ShopifyIntegration](../ShopifyIntegration) sample in this repo,
there's no allow-list of resource names here - Dataverse's Web API uses the
same `/api/data/{version}/{entitySetName}` shape for every table, standard
or custom, so this facade passes whatever name you give it straight
through.

## No SDK

None of this uses the official Dataverse SDK
(`Microsoft.PowerPlatform.Dataverse.Client`/`Microsoft.Xrm.Sdk`) that the
internal tooling this is based on used. Dataverse also exposes a true
REST/OData Web API, so every piece here is a plain `HttpClient` + bearer
token, the same approach used for the Shopify and SmartyStreets
integrations elsewhere in this repo - no SDK dependency, no
`ServiceClient`/`Entity`/`QueryExpression` object model.

## Authentication

All three pieces authenticate to Dataverse with a Microsoft Entra ID app
registration's client credentials (client id/secret/tenant) via
`ClientSecretCredential`. A token alone isn't enough, though - Dataverse
also needs an **Application User** record for that app registration in the
target environment (Power Platform admin center -> your environment ->
Settings -> Users + permissions -> Application users), with a security
role granting the privileges you need. See each piece's README for the
full one-time setup.

## Why PATCH works natively here (unlike Shopify)

Dataverse's Web API updates records via HTTP `PATCH` natively - unlike
Shopify's REST API (which has no PATCH verb and needs a PUT translation),
no verb substitution is needed anywhere in this integration.

## Security note

This is sample/portfolio code. All credentials in this repo are placeholders
(`yourorg.crm.dynamics.com`, `REPLACE_WITH_YOUR_CLIENT_SECRET`, etc.) -
replace them with your own before running anything for real, and never
commit real secrets. `local.settings.json` is git-ignored for exactly that
reason; only `local.settings.json.sample` is checked in.
