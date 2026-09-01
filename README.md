# DotNetCode

Sample .NET code and small class libraries, written to be portfolio-friendly:
each one is based on a real internal utility, generalized and stripped of any
proprietary business logic, credentials, or organization-specific behavior
(documented per-project below, in each one's own "what changed from the
original" section).

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

## Common threads

- **Multi-targeted, not framework-specific.** `ResilientSqlAccess`,
  `CleanValidation`, `IdentityContext`, and `AppDiagnostics` are each one
  project/one codebase targeting `net48;net6.0;net8.0;net10.0` at once -
  everything in them only needs dependencies available on every target.
  `SendingEmailViaMicrosoftGraph` is the exception: its Azure Function is
  .NET 10-only (the isolated worker model), so it ships separate class
  libraries for .NET Framework vs. .NET 6+ callers instead.
- **Graceful degradation over exceptions**, where that fits the problem.
  `CleanValidation`'s parsing helpers return a sensible default (`0`,
  `string.Empty`, `DateTime.MinValue`) instead of throwing, and
  `ResilientSqlAccess` returns a `SqlResult` with `Succeeded`/`ErrorMessage`
  instead of throwing on a SQL failure - so callers check a flag instead of
  wrapping every call in try/catch.
- **Configuration over hardcoding.** Anywhere the original utility baked in
  one organization's specific values (a domain-to-email mapping, a serial
  number format, retryable SQL error codes), the generalized version exposes
  it as a parameter, delegate, or dictionary the caller populates - see each
  project's README for specifics.
- **Every sample runs standalone.** `cd Samples/NetFramework48 && dotnet run`
  or `cd Samples/Net8Plus && dotnet run` inside each project folder - no
  shared solution-wide setup required.

## Security note

All connection strings, credentials, tenant IDs, domain names, and similar
values across this folder are placeholders - replace them with your own
before running anything for real, and never commit real secrets.
