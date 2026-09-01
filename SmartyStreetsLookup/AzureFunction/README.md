# SmartyStreetsLookup Azure Function

An isolated-worker Azure Function (.NET 10) exposing two HTTP endpoints that
validate/normalize a mailing address by calling
[SmartyStreets](https://www.smarty.com/)' US Street and International Street
REST APIs directly (no SmartyStreets SDK dependency - plain HTTP + JSON).

| Endpoint | Calls | Use for |
|---|---|---|
| `POST /UsAddressLookup` | [US Street API](https://www.smarty.com/docs/cloud/us-street-api) | US addresses |
| `POST /InternationalAddressLookup` | [International Street API](https://www.smarty.com/docs/cloud/international-street-api) | Any non-US address |

Both endpoints accept the same request shape and return the same response
shape - pick the endpoint based on which country the address is in.

## Request format

```json
{
  "CompanyName": "Acme Corp",
  "AddressLine1": "1600 Amphitheatre Pkwy",
  "AddressLine2": "",
  "City": "Mountain View",
  "State": "CA",
  "PostalCode": "94043",
  "Country": "US"
}
```

`Country` is required for `/InternationalAddressLookup` (SmartyStreets'
International API needs it to know which country's address format to apply)
and ignored by `/UsAddressLookup`.

## Response format

```json
{
  "IsValid": true,
  "Message": "Found 1 US candidate(s).",
  "CandidateCount": 1,
  "CompanyName": "Acme Corp",
  "AddressLine1": "1600 Amphitheatre Pkwy",
  "AddressLine2": "",
  "City": "Mountain View",
  "State": "CA",
  "PostalCode": "94043-1351",
  "Country": "US"
}
```

`IsValid` is `false` (with an explanatory `Message`, HTTP 200) when
SmartyStreets found no matching candidates, and the request itself is
rejected with HTTP 400 (missing `AddressLine1`, missing `Country` on the
international endpoint, or unparseable JSON) or HTTP 500 (missing
credentials, or an unexpected error calling SmartyStreets).

## Authentication

Both endpoints require an Azure Function key, same as any `AuthorizationLevel.Function`
HTTP trigger:

```
POST https://<your-function-app>/UsAddressLookup?code=<your-function-key>
```
or
```
x-functions-key: <your-function-key>
```

## Required environment variables

| Name | Description |
|---|---|
| `SmartyStreetsUsAuthId` / `SmartyStreetsUsAuthToken` | Credentials for the US Street API |
| `SmartyStreetsInternationalAuthId` / `SmartyStreetsInternationalAuthToken` | Credentials for the International Street API |

Get both pairs from your [SmartyStreets account](https://www.smarty.com/account/keys)
(a free tier is available). See `local.settings.json.sample` for the local
`local.settings.json` shape - copy it and fill in real values, which stays
git-ignored.

## Running locally

```
func start
```

The function listens on `http://localhost:7071` by default; no function key
is required for local requests. Example:

```
curl -X POST http://localhost:7071/UsAddressLookup ^
  -H "Content-Type: application/json" ^
  -d "{\"AddressLine1\":\"1600 Amphitheatre Pkwy\",\"City\":\"Mountain View\",\"State\":\"CA\",\"PostalCode\":\"94043\"}"
```

## Design notes

- **No SDK, no extra JSON library.** Both endpoints call SmartyStreets over
  plain `HttpClient` and parse the response with `System.Text.Json` -
  `Services/SmartyStreetsApiClient.cs` is the entire integration.
- **One retry on HTTP 429** (rate limited), honoring `Retry-After` if
  present, then a fixed 2-second fallback. Any other failure - or a second
  429 - is reported back as a failed `AddressLookupResult` rather than
  thrown, so the endpoint always returns well-formed JSON.
- **Self-contained.** This function does not call or depend on any other
  project in this repository (no shared library reference) - see
  [`../ClassLibraries`](../ClassLibraries) if you want to call SmartyStreets
  directly from your own app instead of through this relay.
