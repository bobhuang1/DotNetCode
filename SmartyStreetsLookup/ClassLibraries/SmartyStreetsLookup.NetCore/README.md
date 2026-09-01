# SmartyStreetsLookup.NetCore

A .NET 6/8/10 class library that calls
[SmartyStreets](https://www.smarty.com/)' US Street and International Street
REST APIs directly over HTTP (`System.Text.Json` for parsing, no
SmartyStreets SDK dependency) - for referencing directly from an existing
.NET 6+ app instead of calling out to a separate relay service.

## Install

Add a project reference (or pack/publish this as a NuGet package yourself)
to `SmartyStreetsLookup.NetCore.csproj`.

## Quick start

```csharp
using SmartyStreetsLookup.NetCore;
using SmartyStreetsLookup.NetCore.Models;

var usClient = new SmartyStreetsUsAddressClient(authId: "...", authToken: "...");

var result = await usClient.LookupAsync(new AddressLookupRequest
{
    AddressLine1 = "1600 Amphitheatre Pkwy",
    City = "Mountain View",
    State = "CA",
    PostalCode = "94043"
});

Console.WriteLine(result.IsValid
    ? $"{result.AddressLine1}, {result.City}, {result.State} {result.PostalCode}"
    : $"Not found: {result.Message}");
```

For a non-US address, use `SmartyStreetsInternationalAddressClient` instead,
and set `Country` on the request (required - SmartyStreets needs it to know
which country's address format to apply):

```csharp
var intlClient = new SmartyStreetsInternationalAddressClient(authId: "...", authToken: "...");

var result = await intlClient.LookupAsync(new AddressLookupRequest
{
    AddressLine1 = "100 Victoria Embankment",
    City = "London",
    PostalCode = "EC4Y 0HQ",
    Country = "GB"
});
```

Get your auth-id/auth-token pairs (one pair per API) from your
[SmartyStreets account](https://www.smarty.com/account/keys) - a free tier
is available. The two APIs use separate credentials.

## Using `IHttpClientFactory` (recommended in apps with a DI container)

Both clients accept an optional `HttpClient` in their constructor. In an
ASP.NET Core app, worker service, or anything else built on the generic
host, get one from `IHttpClientFactory` instead of letting the client create
its own:

```csharp
services.AddHttpClient();
services.AddSingleton(sp => new SmartyStreetsUsAddressClient(
    authId, authToken, sp.GetRequiredService<IHttpClientFactory>().CreateClient()));
```

See `Sample/ConsoleSample.NetCore/Program.cs` for a complete working example
of this pattern.

## Running the sample

```
cd Sample\ConsoleSample.NetCore
set SMARTYSTREETS_US_AUTH_ID=your-us-auth-id
set SMARTYSTREETS_US_AUTH_TOKEN=your-us-auth-token
set SMARTYSTREETS_INTL_AUTH_ID=your-international-auth-id
set SMARTYSTREETS_INTL_AUTH_TOKEN=your-international-auth-token
dotnet run
```

## What's here vs. what's not

This library only wraps the two REST endpoints. It does not include the
country-name-to-ISO-code alias resolution, failure-notification email, or
telemetry pipeline that similar internal tooling this is based on had -
those were either tied to unrelated internal infrastructure or unnecessary:
SmartyStreets' International API already accepts a country name, ISO-2, or
ISO-3 code directly in the `Country` field, so no separate resolution step
is needed.
