# ResilientSqlAccess

A small SQL Server / Azure SQL data-access library with built-in retry-with-backoff
for transient failures. One codebase, one project, targeting **.NET Framework 4.8**
and **.NET 6 / 8 / 10** at the same time via multi-targeting - there's only one
library here because `Microsoft.Data.SqlClient`, `Polly`, and
`Microsoft.Extensions.Logging.Abstractions` all support both worlds.

```
dotnet add package Microsoft.Data.SqlClient   # already a dependency, shown for context
```

## Why this exists

Talking to SQL Server over a real network means occasionally hitting a blip - a
dropped connection, a deadlock, Azure SQL throttling, a failover. Most of those
succeed on a second try a moment later. `ResilientSqlAccess` wraps every call in a
configurable retry policy (via [Polly](https://github.com/App-vNext/Polly)) so you
don't have to hand-roll try/catch/sleep loops around every stored procedure call.

## Project layout

```
ResilientSqlAccess.csproj      Multi-targeted (net48;net6.0;net8.0;net10.0) library
ResilientSqlClient.cs          The main entry point
SqlResult.cs                   Result of a call: value, output params, return value, errors
SqlRetryOptions.cs             Retry count / delay / backoff / which errors to retry
SqlRetryBackoffType.cs         Constant | Linear | Exponential
SqlRetryEventArgs.cs           Payload for the Retrying event
Internal/                      Command-type detection, connection-string redaction, error formatting
Samples/
├── NetFramework48/            Console sample, .NET Framework 4.8
├── Net8Plus/                  Console sample, .NET 8 (also builds against 6/10)
└── Sql/DemoStoredProcedures.sql   Schema + 3 stored procs used by both samples
```

## Quick start

```csharp
using ResilientSqlAccess;

var client = new ResilientSqlClient(connectionString, new SqlRetryOptions
{
    RetryCount = 4,
    RetryDelay = TimeSpan.FromMilliseconds(250),
    BackoffType = SqlRetryBackoffType.Exponential
});

var result = await client.ExecuteScalarAsync("SELECT COUNT(*) FROM dbo.Customers");

if (result.Succeeded)
    Console.WriteLine($"Count: {result.AsInt32()}");
else
    Console.WriteLine($"Failed: {result.ErrorMessage}");
```

`ResilientSqlClient` has three methods, all `async`, all accepting either raw SQL
text or a bare stored procedure name (it auto-detects which one you passed - see
[Stored procedures](#stored-procedures--output-parameters--return-values) below):

| Method | Use for | `SqlResult.Result` type |
|---|---|---|
| `ExecuteScalarAsync` | A single value - `SELECT COUNT(*)`, a lookup, a procedure's first column | `object` (read via `AsInt32()`, `AsDecimal()`, `AsString()`, etc.) |
| `ExecuteQueryAsync` | A query that returns rows | `DataSet` (read via `AsDataTable()` for the first table, `AsDataSet()` for all of them) |
| `ExecuteNonQueryAsync` | `INSERT`/`UPDATE`/`DELETE`, or a procedure that doesn't select rows | `object` boxing an `int` (rows affected), via `AsInt32()` |

Every call returns a `SqlResult` instead of throwing on a SQL failure -
`result.Succeeded` / `result.IsCriticalError` tells you whether it worked, and
`result.ErrorMessage` has a diagnostic message safe to log (connection strings are
never included in it). Failures unrelated to SQL (a bad connection string, a
cancelled token, etc.) still surface as `IsCriticalError = true` with the
exception's message.

## Configuring retries

```csharp
var options = new SqlRetryOptions
{
    RetryCount = 5,                                 // attempts AFTER the first try
    RetryDelay = TimeSpan.FromSeconds(1),            // base delay used by the backoff formula
    BackoffType = SqlRetryBackoffType.Exponential,   // Constant | Linear | Exponential
    CommandTimeoutSeconds = 60                       // optional, defaults to ADO.NET's 30s
};
```

### Backoff types

The delay before attempt *n* (1-based) is computed from `RetryDelay`:

| `BackoffType` | Formula | Example with `RetryDelay = 1s` |
|---|---|---|
| `Constant` | `RetryDelay` | 1s, 1s, 1s, 1s, ... |
| `Linear` | `RetryDelay * attempt` | 1s, 2s, 3s, 4s, ... |
| `Exponential` | `RetryDelay * 2^(attempt-1)` | 1s, 2s, 4s, 8s, ... |

See `SqlRetryOptions.GetDelayForAttempt(int attempt)` for the implementation. This
was verified against a real (unreachable) SQL Server during development - with
`RetryDelay = 250ms` and `BackoffType = Exponential`, actual observed retry delays
were 250ms, 500ms, 1s, 2s, matching the formula exactly.

### Which errors get retried

Every `SqlException` is retried **except** the error numbers listed in
`SqlRetryOptions.NonRetryableErrorNumbers` (default: `-1`, `233`, `18456`, `2` -
connection failures and login failures, where retrying can't possibly help).

This is deliberately an opt-out deny-list rather than an opt-in allow-list of known
transient error codes: an error you've never seen before (a new Azure SQL
throttling code, an unfamiliar network blip) is retried by default instead of
silently failing fast. If you want a stricter allow-list instead, or need
different logic entirely (e.g. only retry on read operations), set
`SqlRetryOptions.IsTransient` - when set, it takes priority over
`NonRetryableErrorNumbers`:

```csharp
options.IsTransient = ex => ex.Number is 4060 or 40197 or 40501 or 40613 or 49918 or 1205;
```

### Observing retries

Two ways to see retries happen, pick whichever fits your app:

```csharp
// 1) ILogger (recommended for ASP.NET Core / worker services / anything with DI)
var client = new ResilientSqlClient(connectionString, options, logger);
// logs a Warning per retry attempt and an Error if all retries are exhausted

// 2) Event (handy with no DI container, e.g. a plain console app or WinForms/WPF)
client.Retrying += (_, e) =>
    Console.WriteLine($"{e.OperationName} attempt {e.Attempt}/{e.MaxAttempts}, waiting {e.Delay}");
```

## Stored procedures, output parameters & return values

Pass a bare procedure name (no spaces, no SQL keywords) and it's automatically run
as `CommandType.StoredProcedure`; anything else (starts with `SELECT`, `INSERT`,
`WITH`, has a space, etc.) runs as `CommandType.Text`. You never set
`CommandType` yourself.

```csharp
using var command = ...; // NOT needed - just pass parameters straight to the client
```

### Input parameters

```csharp
var result = await client.ExecuteQueryAsync(
    "usp_GetCustomersByName",
    new[] { new SqlParameter("@NamePattern", SqlDbType.NVarChar, 200) { Value = "A%" } });
```

### Output parameters

Give the `SqlParameter` `Direction = ParameterDirection.Output` (or
`InputOutput`). If you don't set `.Size` on a variable-length type, the client
fills in a default of 4000 (configurable via `SqlRetryOptions.OutputParameterDefaultSize`)
- SQL Server output parameters require a size and it's easy to forget.

```sql
CREATE OR ALTER PROCEDURE dbo.usp_InsertCustomer
    @Name           NVARCHAR(200),
    @Email          NVARCHAR(320),
    @NewCustomerId  INT OUTPUT
AS
BEGIN
    INSERT INTO dbo.Customers (Name, Email) VALUES (@Name, @Email);
    SET @NewCustomerId = CAST(SCOPE_IDENTITY() AS INT);
    RETURN 0;
END
```

```csharp
var result = await client.ExecuteNonQueryAsync(
    "usp_InsertCustomer",
    new[]
    {
        new SqlParameter("@Name", SqlDbType.NVarChar, 200) { Value = "Ada Lovelace" },
        new SqlParameter("@Email", SqlDbType.NVarChar, 320) { Value = "ada@example.com" },
        new SqlParameter("@NewCustomerId", SqlDbType.Int) { Direction = ParameterDirection.Output }
    });

if (result.Succeeded)
{
    int newId = result.GetOutputParameter<int>("@NewCustomerId");
}
```

`SqlResult.OutputParameters` is a `Dictionary<string, object?>` of every
Output/InputOutput parameter's final value if you'd rather read it directly;
`GetOutputParameter<T>` is a typed convenience wrapper over it (works with or
without the leading `@`).

### Return values

Stored procedures always get an automatically-added `RETURN_VALUE` parameter if
you didn't supply one yourself, so `SqlResult.ReturnValue` is populated for every
stored procedure call - use it for the common pattern of returning a status code
(`0` = success, non-zero = a specific business outcome) without throwing an
exception for an expected case:

```csharp
if (result.Succeeded && result.ReturnValue == 0)
{
    // inserted
}
else if (result.Succeeded && result.ReturnValue == 1)
{
    // e.g. "duplicate email" - a business case, not a SQL error
}
```

`ReturnValue` is `null` for plain SQL text commands (it only applies to stored procedures).

### Multiple result sets

`ExecuteQueryAsync` loads every result set a procedure returns into
`SqlResult.AsDataSet()` (`Table0`, `Table1`, ...); `AsDataTable()` is a shortcut
for the first one, which covers the common single-result-set case.

## Running the samples

1. Point a scratch SQL Server / Azure SQL database at
   `Samples/Sql/DemoStoredProcedures.sql` (creates a `Customers` table and 3
   procedures used by both samples):

   ```
   sqlcmd -S localhost -E -d YourScratchDb -i Samples/Sql/DemoStoredProcedures.sql
   ```

2. Set the connection string (both samples fall back to a `localhost` /
   Integrated Security default if you skip this):

   ```
   set RESILIENT_SQL_CONNECTION_STRING=Server=localhost;Database=YourScratchDb;Integrated Security=true;TrustServerCertificate=true;
   ```

3. Run either sample:

   ```
   cd Samples/NetFramework48 && dotnet run
   cd Samples/Net8Plus && dotnet run
   ```

Both samples exercise all three methods (`ExecuteScalarAsync`,
`ExecuteQueryAsync`, `ExecuteNonQueryAsync`) against the demo procedures,
including the output-parameter/return-value insert. The .NET Framework sample
observes retries via the `Retrying` event; the .NET 8 sample observes them via an
injected `ILogger` (registered through the generic host's DI container) - see
[Observing retries](#observing-retries) above.

## Design notes

- Each call opens and closes its own `SqlConnection` (relying on ADO.NET's
  built-in connection pooling, the standard pattern) rather than holding a
  connection open across calls - this keeps the retry policy able to fully
  re-establish a broken connection on the next attempt.
- No jitter is added to the backoff delays. For high-concurrency scenarios where
  many callers might retry in lockstep, consider layering
  [Polly.Contrib.WaitAndRetry](https://github.com/Polly-Contrib/Polly.Contrib.WaitAndRetry)'s
  jitter strategies on top, or randomizing `RetryDelay` slightly per client instance.
- `SqlResult` never throws for a SQL failure - check `Succeeded`/`IsCriticalError`.
  Non-SQL exceptions (cancellation, programming errors constructing parameters,
  etc.) are not swallowed the same way; `OperationCanceledException` in particular
  propagates normally so cancellation isn't reported as a SQL error.
