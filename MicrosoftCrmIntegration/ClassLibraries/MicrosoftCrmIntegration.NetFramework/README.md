# MicrosoftCrmIntegration.NetFramework

A .NET Framework 4.8 class library that calls the
[Microsoft Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
(the REST/OData API behind Dynamics 365 CE / Power Platform's "Microsoft
CRM") directly over HTTP for CRUD operations against **any table** -
accounts, contacts, incidents (support cases), products, price lists
(`pricelevels`), or a custom one. No Dataverse SDK dependency
(`Microsoft.PowerPlatform.Dataverse.Client`/`Microsoft.Xrm.Sdk`); this is a
thin REST client, and request/response bodies are passed through as raw
JSON exactly as Dataverse's Web API defines them, so it stays generic
instead of reimplementing Dataverse's schema.

## Install

Add a project reference (or pack/publish this as a NuGet package yourself)
to `MicrosoftCrmIntegration.NetFramework.csproj`.

## One-time setup: an app registration + Application User

Dataverse's Web API authenticates with a Microsoft Entra ID (Azure AD)
token, same as any other Azure-protected API - but a token alone isn't
enough; Dataverse also needs to know *who* that app registration is inside
your specific environment:

1. **Entra ID -> App registrations -> New registration.** Note the
   **Application (client) ID** and **Directory (tenant) ID**.
2. **Certificates & secrets -> New client secret.** Note the secret value
   (shown once).
3. **Power Platform admin center -> your environment -> Settings -> Users +
   permissions -> Application users -> New app user.** Add the app
   registration by its Application ID, and assign it a security role (e.g.
   a custom role scoped to just the tables/privileges you need, or
   System Administrator for a quick test). Without this step you'll get a
   401/403 even with a valid token - the token proves *authentication*,
   the Application User record grants *authorization* inside Dataverse.

No API permissions need to be added in the app registration's own "API
permissions" blade for this client-credentials flow - the Application User
step above is what grants access.

## Quick start

```csharp
using MicrosoftCrmIntegration.NetFramework;

var client = new DataverseClient(
    resourceUrl: "https://yourorg.crm.dynamics.com",
    tenantId: "...", clientId: "...", clientSecret: "...");

// GET (list) - OData query options passed straight through
var list = await client.ListAsync("accounts", "$select=name,accountnumber&$filter=statecode eq 0&$top=25");
Console.WriteLine(list.Body); // raw JSON: { "@odata.context": "...", "value": [ ... ] }

// GET (single)
var one = await client.GetAsync("contacts", contactId, "$select=fullname,emailaddress1");

// POST (create) - body is exact Dataverse column/value JSON
var created = await client.CreateAsync("contacts", @"{
    ""firstname"": ""Jane"", ""lastname"": ""Doe"", ""emailaddress1"": ""jane.doe@example.com""
}");
Guid? newId = created.CreatedEntityId;

// PATCH (update) - Dataverse's native update verb, no translation needed
var updated = await client.UpdateAsync("contacts", contactId, @"{ ""lastname"": ""Smith"" }");

// DELETE
var deleted = await client.DeleteAsync("contacts", contactId);
```

Every call returns a `DataverseApiResult`:

```csharp
public sealed class DataverseApiResult
{
    public bool IsSuccess { get; set; }
    public int StatusCode { get; set; }          // Dataverse's HTTP status, or 0 if the request never reached Dataverse
    public string Body { get; set; }             // raw JSON Dataverse returned (success or error)
    public Guid? CreatedEntityId { get; set; }   // parsed from the OData-EntityId header on a successful create
    public string? ErrorMessage { get; set; }
}
```

Deserialize `Body` into whatever model fits your app - this library
intentionally doesn't ship one.

## Common tables ("customer, product, price, support case")

| Concept | Entity set name | Notes |
|---|---|---|
| Customer (company) | `accounts` | |
| Customer (person) | `contacts` | |
| Support case | `incidents` | Dataverse's "Case" entity is internally named `incident` |
| Product | `products` | |
| Price list | `pricelevels` | Line-item prices live in the related `productpricelevels` table |

These are just the common examples - **any** table name works, standard or
custom, since Dataverse's Web API uses the same `/api/data/{version}/{entitySetName}`
shape for all of them. This client does not restrict which names you pass.

## Sharing an `HttpClient`

The constructor accepts an optional `HttpClient`. If your app already keeps
a single, long-lived `HttpClient` around (the recommended pattern on .NET
Framework, to avoid socket exhaustion from creating one per request), pass
it in and the client will reuse it instead of creating its own:

```csharp
var sharedClient = new HttpClient();
var client = new DataverseClient(resourceUrl, tenantId, clientId, clientSecret, httpClient: sharedClient);
```

## TLS 1.2

The client sets `ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12`
in a static constructor, since TLS 1.2 is not always the OS/.NET Framework
default on older Windows machines and both Entra ID and Dataverse require
it. You don't need to set this yourself.

## Token handling

Each call acquires a token via `ClientSecretCredential.GetTokenAsync` -
`Azure.Identity` credentials cache and refresh tokens internally and are
documented as thread-safe, so there's no extra caching layer here.

## Rate limiting

Dataverse's Web API throttles with HTTP 429 and a `Retry-After` header,
same as Shopify's REST API. This client retries once, honoring
`Retry-After` if present (a fixed 2-second fallback otherwise); a second
429 is returned to you as a normal (non-success) `DataverseApiResult`
rather than thrown.

## Running the sample

```
cd Sample\ConsoleSample.NetFramework
set DATAVERSE_URL=https://yourorg.crm.dynamics.com
set DATAVERSE_TENANT_ID=your-tenant-id
set DATAVERSE_CLIENT_ID=your-client-id
set DATAVERSE_CLIENT_SECRET=your-client-secret
dotnet run
```
