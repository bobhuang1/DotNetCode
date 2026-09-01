# CleanValidation

A small, dependency-light (BCL only) library of null-tolerant text/number/date
cleaning, validation, and formatting helpers - the kind of code that sits between
raw user/external input and your business logic so the rest of your codebase
never has to null-check or try/catch a parse. One project, multi-targeted for
**.NET Framework 4.8** and **.NET 6 / 8 / 10** at once - every method here only
needs the BCL (plus `System.Text.Json`, which net48 pulls in as a package since
it ships in the box from net6.0 onward).

## Design philosophy

Every method degrades gracefully instead of throwing: `Int32("abc")` returns
`0`, `Date(null)` returns `DateTime.MinValue`, `Text(null)` returns
`string.Empty`. This is a deliberate trade-off for input-cleaning code - the
caller decides what "empty/zero" means for their case, rather than every call
site needing its own try/catch around a parse.

## Quick start

```csharp
using CV = CleanValidation.CleanValidation;

int quantity = CV.Int32(request.Form["qty"]);          // "  42px " -> 42, "" -> 0
decimal price = CV.Decimal(request.Form["price"]);       // "$1,234.567" -> 1234.57 (2dp)
bool isActive = CV.Bit(request.Form["active"]);           // "true"/"1" -> true
DateTime placedOn = CV.Date(request.Form["date"]);

if (!CV.ValidateEmail(email)) return BadRequest("Invalid email.");
```

`using CV = CleanValidation.CleanValidation;` sidesteps the fact that the
namespace and the static class share a name - a plain `using CleanValidation;`
also compiles, but you'd need to write `CleanValidation.CleanValidation.Int32(...)`.

## What's here

| Category | Methods |
|---|---|
| Text | `Text`, `Nbsp`, `TrimLongString`, `Number`, `HtmlLineBreaks`, `ReplaceIgnoreCase` |
| Numbers / dates / bools | `Int32`, `Int64`, `Decimal`, `Float`, `Bit`, `Date`, `ShortDateString`, `BoolToString` |
| Hardware / retail identifiers | `BluetoothAddress`, `ValidateBluetoothAddress`, `Upc` |
| Validation | `ValidateEmail` |
| URLs | `NormalizeUrl` |
| Files / Excel | `FileName`, `ExcelSheetName`, `StripExtension`, `EnsureExtension` |
| JSON / enums | `PrettyJson`, `EnumName` |
| Formatting / hashing / keys | `FormatCurrency`, `GetMd5Hash`, `RandomKey` |
| Token lists | `ParseTokenList`, `ParseCommaSeparatedList`, `JoinTokenList`, `SplitCommaSeparatedList` |
| Structured codes | `ExtractStructuredCodeSegment`, `SortByStructuredCodeSegmentDescending` |
| Reflection | `FlattenObjectProperties` |

A couple worth calling out:

- **`GetMd5Hash`** - MD5 is cryptographically broken. This is included for
  non-security uses only (cache keys, checksums, dedup fingerprints) - never for
  passwords, tokens, or anything where collision resistance matters. Use
  `System.Security.Cryptography.SHA256`/a proper password hasher (e.g.
  `Rfc2898DeriveBytes`/BCrypt) for anything security-sensitive.
- **`ReplaceIgnoreCase`** - .NET Framework 4.8 has no
  `string.Replace(string, string, StringComparison)` overload (that was added in
  .NET Core). This method exists specifically to fill that gap for net48
  callers; on net6+, prefer the built-in overload.
- **`ExtractStructuredCodeSegment` / `SortByStructuredCodeSegmentDescending`** -
  a generic, parameterized way to pull a fixed-position segment out of a
  fixed-length code (serial number, SKU, lot code, whatever your domain uses)
  and sort a list by it. Pass in your own length/offset values instead of the
  library hardcoding a specific format.

## Scope

This library deliberately stays focused on cleaning/validating/formatting
values that are already in memory - a few related concerns are intentionally
out of scope:

| Not included | Why |
|---|---|
| SQL string-literal escaping | Escaping text for inline SQL string literals is a SQL-injection-shaped foot-gun. Every real call site should use parameterized queries instead (see the [ResilientSqlAccess](../ResilientSqlAccess) library) - this library shouldn't offer an easy path around that. |
| UI control helpers (e.g. dropdown selection) | Tied to `System.Web.UI.WebControls.DropDownList`, a WebForms-only type with no .NET 6+ equivalent, and out of scope for a data-cleaning library regardless of framework. The equivalent one-liner for any `IEnumerable` of options is `items.FirstOrDefault(i => i.Value == selectedValue)`. |

Two methods are generalized rather than tied to one specific format:

- **`ExtractStructuredCodeSegment` / `SortByStructuredCodeSegmentDescending`**
  pull a fixed-position segment out of a fixed-length code (serial number,
  SKU, lot code, whatever your domain uses) and sort a list by it - pass in
  your own length/offset values instead of the library assuming a specific
  layout.
- **`NormalizeUrl`**'s optional `preserveAnnotationSuffix` parameter lets a
  caller supply their own "unconfirmed value" marker convention (e.g. a
  trailing `"(guess)"` some upstream process attaches) instead of one being
  baked into the library.

## Running the samples

```
cd Samples/NetFramework48 && dotnet run
cd Samples/Net8Plus && dotnet run
```

Both samples call the same set of methods and print identical output - the
whole point of this library being framework-agnostic.
