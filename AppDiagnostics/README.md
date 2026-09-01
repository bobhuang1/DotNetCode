# AppDiagnostics

A small static facade over debug output / an optional `ILogger`, plus
exception-formatting and JS-string-escaping helpers. One project, multi-targeted
for **.NET Framework 4.8** and **.NET 6 / 8 / 10** at once - everything here
only needs `System.Diagnostics.Debug` and `Microsoft.Extensions.Logging.Abstractions`,
both available on every target.

## Quick start

```csharp
using AppDiagnostics;

// Both outputs are opt-in and off by default - referencing this library produces
// no output on its own.
AppDiagnostics.EnableDebugOutput = true;                 // -> System.Diagnostics.Debug
AppDiagnostics.EnableLogOutput = true;
AppDiagnostics.Logger = myServiceProvider.GetRequiredService<ILogger<Worker>>();

AppDiagnostics.Log(nameof(Worker), "Starting up.");

try
{
    DoWork();
}
catch (Exception ex)
{
    AppDiagnostics.Log(nameof(Worker), ex, isError: true);
    // or, if you just want the formatted string yourself:
    var text = AppDiagnostics.GetStandardException(ex);
}
```

(Reference it as `AppDiagnostics.AppDiagnostics.Log(...)`, or add
`using A = AppDiagnostics.AppDiagnostics;`, if you'd rather not rely on the
namespace/class name resolving the same way `CleanValidation` does - see that
library's README for the full explanation of the pattern.)

## What's here

| Member | Purpose |
|---|---|
| `EnableDebugOutput`, `EnableLogOutput`, `Logger` | Global on/off switches and the logger sink `Log` writes to. All off/null by default. |
| `Log(source, message, isError = false)` | Unified debug/log output - writes to `Debug` and/or `Logger` depending on the switches above. `message` can be a string or an `Exception` (formatted via `GetStandardException`). |
| `GetStandardException(Exception)` | One-line-friendly rendering of any exception: message, source, stack trace, data. |
| `GetTimeoutException(TimeoutException)` | Same rendering, kept as a distinctly named overload for timeout-specific call sites. |
| `EscapeJsString(string?)` | Escapes a string for safe embedding inside a JS string literal - guards against both breaking out of the string literal and a literal `</script>` terminating the enclosing block early. |

## What changed from the original

| Removed / changed | Why |
|---|---|
| `GetFaultException(FaultException<Microsoft.Xrm.Sdk.OrganizationServiceFault>)` | Required a Dynamics 365/CRM SDK dependency for one narrow method. Rather than pull a WCF+CRM dependency into every consumer of this library, it's dropped - if you need WCF fault formatting, `GetStandardException(ex)` covers the base exception fields, and you can append `ex.Detail`-specific fields yourself at the call site. |
| `MessageBox(text, redirectUrl)` | Wrote an inline `<script>alert(...)</script>` via classic ASP.NET's `HttpContext.Current.Response`. Tied to `System.Web` (no .NET 6+ equivalent) and a dated pattern even on .NET Framework - modern apps should use a proper notification/toast mechanism in their UI framework. `EscapeJsString` (kept) is the reusable piece if you're rolling your own. |
| `SetSelectedValue(DropDownList, ...)` | Tied to `System.Web.UI.WebControls.DropDownList` (WebForms-only) and duplicated verbatim in the original `CUtilityCleanValidation` - out of scope for a diagnostics library on any framework. |
| `GetScreenOutputText` | Overlapped with `AddHtmlLineBreak` in the sibling [CleanValidation](../CleanValidation) library. Text-transformation helpers now live there; this library only formats exceptions and JS strings. |
| `DeveloperEmailAddress` constant | A hardcoded personal email address with no functional use in the file - removed. |
| `ShowInfoAndError` -> `Log` | Renamed: "Show" implied UI display, but the method only ever wrote to `Debug`/`ILogger`. |

## Running the samples

```
cd Samples/NetFramework48 && dotnet run
cd Samples/Net8Plus && dotnet run
```

Both wire up `Microsoft.Extensions.Logging.Console` as the `Logger`, throw a
sample exception, and print the formatted output plus an `EscapeJsString`
example - output is identical on both frameworks.
