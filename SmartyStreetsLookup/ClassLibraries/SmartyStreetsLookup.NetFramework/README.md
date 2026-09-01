# SmartyStreetsLookup.NetFramework

A .NET Framework 4.8 class library that calls
[SmartyStreets](https://www.smarty.com/)' US Street and International Street
REST APIs directly over HTTP (`Newtonsoft.Json` for parsing, no SmartyStreets
SDK dependency) - for referencing directly from an existing .NET Framework
app (console, WinForms/WPF, ASP.NET, Windows service) instead of calling out
to a separate relay service.

## Install

Add a project reference (or pack/publish this as a NuGet package yourself)
to `SmartyStreetsLookup.NetFramework.csproj`.

## Quick start

```csharp
using SmartyStreetsLookup.NetFramework;
using SmartyStreetsLookup.NetFramework.Models;

var usClient = new SmartyStreetsUsAddressClient(authId: "...", authToken: "...");

var result = await usClient.LookupAsync(new AddressLookupRequest
{
    AddressLine1 = "1600 Amphitheatre Pkwy",
    City = "Mountain View",
    State = "CA",
    PostalCode = "94043"
});

if (result.IsValid)
    Console.WriteLine(result.AddressLine1 + ", " + result.City + ", " + result.State + " " + result.PostalCode);
else
    Console.WriteLine("Not found: " + result.Message);
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

## Sharing an `HttpClient`

Both clients accept an optional `HttpClient` in their constructor. If your
app already keeps a single, long-lived `HttpClient` around (the recommended
pattern on .NET Framework, to avoid socket exhaustion from creating one per
request), pass it in and both clients will reuse it instead of creating
their own:

```csharp
var sharedClient = new HttpClient();
var usClient = new SmartyStreetsUsAddressClient(authId, authToken, sharedClient);
```

## TLS 1.2

Both clients set `ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12`
in a static constructor, since TLS 1.2 is not always the OS/.NET Framework
default on older Windows machines and SmartyStreets' API requires it. You
don't need to set this yourself.

## Running the sample

```
cd Sample\ConsoleSample.NetFramework
set SMARTYSTREETS_US_AUTH_ID=your-us-auth-id
set SMARTYSTREETS_US_AUTH_TOKEN=your-us-auth-token
set SMARTYSTREETS_INTL_AUTH_ID=your-international-auth-id
set SMARTYSTREETS_INTL_AUTH_TOKEN=your-international-auth-token
dotnet run
```

## Scope

This library only wraps the two REST endpoints - it does not include
country-name-to-ISO-code alias resolution, failure-notification email, or a
telemetry pipeline. A separate country-resolution step isn't needed:
SmartyStreets' International API already accepts a country name, ISO-2, or
ISO-3 code directly in the `Country` field.
