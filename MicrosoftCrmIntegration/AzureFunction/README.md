# MicrosoftCrmIntegration Azure Function

An isolated-worker Azure Function (.NET 10) exposing generic HTTP endpoints
for CRUD operations against the
[Microsoft Dataverse Web API](https://learn.microsoft.com/power-apps/developer/data-platform/webapi/overview)
- the REST/OData API behind Dynamics 365 CE / Power Platform's "Microsoft
CRM". This is a thin proxy: it does not model Dataverse's schema itself,
and works against **any table** (standard or custom) - request/response
bodies are Dataverse's own JSON, passed through unmodified in both
directions.

## Endpoints

| Method | Route | Dataverse call |
|---|---|---|
| `GET` | `/dataverse/{entitySet}` | List (OData query string forwarded as-is, e.g. `?$select=name&$filter=statecode eq 0&$top=25`) |
| `GET` | `/dataverse/{entitySet}/{id}` | Get one by id (`{id}` is a GUID) |
| `POST` | `/dataverse/{entitySet}` | Create |
| `PATCH` | `/dataverse/{entitySet}/{id}` | Update - Dataverse's native update verb, no translation needed |
| `DELETE` | `/dataverse/{entitySet}/{id}` | Delete |

`{entitySet}` is the Dataverse entity set (table) name - any table works,
standard or custom, since Dataverse's Web API uses the same
`/api/data/{version}/{entitySetName}` shape for all of them. Common
examples ("customer, product, price, support case"):

| Concept | Entity set name |
|---|---|
| Customer (company) | `accounts` |
| Customer (person) | `contacts` |
| Support case | `incidents` (Dataverse's "Case" entity is internally named `incident`) |
| Product | `products` |
| Price list | `pricelevels` |

A malformed `{id}` (not a GUID) returns HTTP 400 without calling Dataverse.

## Request/response bodies

Bodies are Dataverse's exact JSON shape - this proxy does not wrap, unwrap,
or validate them beyond forwarding. For `POST`, send the column/value JSON
for the table:

```json
{ "firstname": "Jane", "lastname": "Doe", "emailaddress1": "jane.doe@example.com" }
```

`POST` requests are sent with `Prefer: return=representation`, so a
successful create returns HTTP 201 with the full new record as the
response body (rather than Dataverse's default 204 No Content). The
response is otherwise Dataverse's own JSON body and HTTP status code,
passed straight through - including Dataverse's own 4xx/5xx errors, so a
non-2xx response from this endpoint usually means Dataverse itself
rejected the request, not this proxy.

This proxy only produces its own error body (`{"error": "..."}`) for three
cases: a malformed `{id}` (400), missing server-side credentials (500), or
a network-level failure reaching Dataverse at all (502).

## Authentication

Two layers:

1. **This Function** - an Azure Function key, same as any
   `AuthorizationLevel.Function` HTTP trigger (`?code=...` or
   `x-functions-key` header).
2. **Dataverse** - a Microsoft Entra ID app registration's client
   credentials (client id/secret/tenant), configured server-side via
   environment variables (never sent by the caller). A token alone isn't
   enough, though - Dataverse also needs an **Application User** record for
   that app registration in the target environment:

   1. **Entra ID -> App registrations -> New registration.** Note the
      Application (client) ID and Directory (tenant) ID.
   2. **Certificates & secrets -> New client secret.**
   3. **Power Platform admin center -> your environment -> Settings ->
      Users + permissions -> Application users -> New app user.** Add the
      app registration by its Application ID and assign it a security
      role. Without this step you'll get a 401/403 even with a valid
      token - the token proves *authentication*, the Application User
      record grants *authorization* inside Dataverse.

## Required environment variables

| Name | Description |
|---|---|
| `DataverseUrl` | e.g. `https://yourorg.crm.dynamics.com` |
| `DataverseTenantId` | Microsoft Entra tenant id |
| `DataverseClientId` | App registration's application (client) id |
| `DataverseClientSecret` | App registration's client secret |
| `DataverseApiVersion` | Optional; defaults to `v9.2`, the long-standing stable Dataverse Web API version |

See `local.settings.json.sample` for the local `local.settings.json` shape
- copy it and fill in real values, which stays git-ignored.

## Running locally

```
func start
```

```
curl -X POST http://localhost:7071/dataverse/contacts ^
  -H "Content-Type: application/json" ^
  -d "{\"firstname\":\"Jane\",\"lastname\":\"Doe\",\"emailaddress1\":\"jane.doe@example.com\"}"

curl "http://localhost:7071/dataverse/accounts?%24select=name%2Caccountnumber&%24top=5"
curl http://localhost:7071/dataverse/contacts/<guid>
curl -X PATCH http://localhost:7071/dataverse/contacts/<guid> ^
  -H "Content-Type: application/json" ^
  -d "{\"lastname\":\"Smith\"}"
curl -X DELETE http://localhost:7071/dataverse/contacts/<guid>
```

(`%24` is a URL-encoded `$`, needed because OData query options like
`$select` and `$top` start with a character that has special meaning in a
shell/URL otherwise.)

## Design notes

- **No SDK.** Every call goes straight to Dataverse's Web API over
  `HttpClient` - no `Microsoft.PowerPlatform.Dataverse.Client` or
  `Microsoft.Xrm.Sdk` dependency, and no JSON parsing library either, since
  bodies are forwarded as raw strings in both directions. `Azure.Identity`
  is used only for token acquisition (`ClientSecretCredential`).
- **One retry on HTTP 429** (Dataverse's rate limit), honoring
  `Retry-After` if present, then a fixed 2-second fallback. A failure to
  reach Dataverse at all (DNS, timeout, etc.) is reported as HTTP 502
  rather than thrown.
- **Self-contained.** This function does not call or depend on any other
  project in this repository (no shared library reference) - see
  [`../ClassLibraries`](../ClassLibraries) if you want to call Dataverse
  directly from your own app instead of through this relay.
