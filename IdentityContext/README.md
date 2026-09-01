# IdentityContext

Helpers for the current Windows/domain identity, plus (via a small seam
interface) the current web request's authenticated user, client IP, and site
base URL. One project, multi-targeted for **.NET Framework 4.8** and
**.NET 6 / 8 / 10** at once.

## Why one library covers both frameworks despite `HttpContext` not being portable

Classic ASP.NET's `System.Web.HttpContext` and ASP.NET Core's
`Microsoft.AspNetCore.Http.HttpContext` are unrelated types from unrelated
assemblies - there's no shared base type, and `System.Web` doesn't exist outside
.NET Framework at all. Rather than fork this library in two, or drag a specific
web framework in as a dependency, the parts of `DomainIdentity` that need "the
current request" take a tiny interface, **`IWebRequestContext`**, instead of a
concrete `HttpContext`:

```csharp
public interface IWebRequestContext
{
    string? UserIdentityName { get; }
    string? ForwardedForHeader { get; }
    string? RemoteAddress { get; }
    string? Scheme { get; }
    string? Authority { get; }
    string? ApplicationPath { get; }
}
```

Everything that doesn't need a web request at all - `IsDevMachine`,
`GetCurrentWindowsLogonName`, `SplitDomainLogon`, `EscapeLogonForUrl` - works
with zero setup in any app (web or not). The two methods that do need a request
(`GetClientIpAddress`, `GetSiteBaseUrl`) - plus `GetCurrentUserDomainEmail`,
which optionally uses one - take an `IWebRequestContext`, which you implement
once per platform with a ~6-line adapter:

### ASP.NET (System.Web) adapter

```csharp
using System.Web;
using IdentityContext;

public sealed class SystemWebRequestContext : IWebRequestContext
{
    public string? UserIdentityName => HttpContext.Current?.User?.Identity?.Name;
    public string? ForwardedForHeader => HttpContext.Current?.Request?.ServerVariables?["HTTP_X_FORWARDED_FOR"];
    public string? RemoteAddress => HttpContext.Current?.Request?.ServerVariables?["REMOTE_ADDR"];
    public string? Scheme => HttpContext.Current?.Request?.Url?.Scheme;
    public string? Authority => HttpContext.Current?.Request?.Url?.Authority;
    public string? ApplicationPath => HttpContext.Current?.Request?.ApplicationPath;
}

// usage, anywhere HttpContext.Current is available:
var ip = DomainIdentity.GetClientIpAddress(new SystemWebRequestContext());
```

### ASP.NET Core adapter

```csharp
using Microsoft.AspNetCore.Http;
using IdentityContext;

public sealed class AspNetCoreRequestContext(HttpContext httpContext) : IWebRequestContext
{
    public string? UserIdentityName => httpContext.User?.Identity?.Name;
    public string? ForwardedForHeader => httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    public string? RemoteAddress => httpContext.Connection.RemoteIpAddress?.ToString();
    public string? Scheme => httpContext.Request.Scheme;
    public string? Authority => httpContext.Request.Host.Value;
    public string? ApplicationPath => httpContext.Request.PathBase.Value;
}

// usage, e.g. in a minimal API handler or controller action:
app.MapGet("/whoami", (HttpContext ctx) =>
    DomainIdentity.GetCurrentUserLogonName(new AspNetCoreRequestContext(ctx)));
```

(Registering `AspNetCoreRequestContext` as a scoped DI service backed by
`IHttpContextAccessor` is the usual next step if you want it injectable outside
a request-handling delegate.)

## Quick start (no web context needed)

```csharp
using IdentityContext;

bool isDev = DomainIdentity.IsDevMachine();
string logon = DomainIdentity.GetCurrentWindowsLogonName();      // "DOMAIN\username"
var (domain, userName) = DomainIdentity.SplitDomainLogon(logon);
```

## Domain-to-email mapping

`GetCurrentUserDomainEmail` turns a domain logon into a best-effort email
address. It's inherently organization-specific, so it's built as an
extensibility point rather than hardcoded behavior:

```csharp
// Configure once at startup for your organization's domains:
DomainIdentity.DomainToEmailSuffixMap["CONTOSO"] = "@contoso.example.com";
DomainIdentity.DomainToEmailSuffixMap["FABRIKAM"] = "@fabrikam.example.com";

// Optional: override the resolved username for specific domains (e.g. a shared
// kiosk/service account that should always map to one mailbox).
DomainIdentity.UserNameOverrideResolver = domain => domain == "KIOSK" ? "front-desk" : null;

var email = DomainIdentity.GetCurrentUserDomainEmail(); // "jane.doe" @ "CONTOSO\jane.doe" -> "jane.doe@contoso.example.com"
```

If the domain isn't in the map, it falls back to guessing
`username@{domain}.com` (or `username@{domain}` if the domain already looks like
a hostname) - a reasonable default, but you should populate the map for domains
you actually care about.

## What changed from the original

The internal utility this is based on had a `GetCurrentUserDomainEmail` with
one specific company's domain names and even a specific machine-name-to-username
override hardcoded directly in the method body - proprietary and meaningless
outside that organization. It's been rebuilt as the configurable
`DomainToEmailSuffixMap` / `UserNameOverrideResolver` shown above: same shape
and purpose (resolve an email address from a domain logon), fully
organization-agnostic, empty by default.

## Running the samples

```
cd Samples/NetFramework48 && dotnet run
cd Samples/Net8Plus && dotnet run
```

Both exercise the framework-agnostic methods directly (using the real current
Windows identity) and the web-context methods against a fake
`IWebRequestContext` (there's no real `HttpContext` in a console app) - see the
adapters above for real usage in a web project.
