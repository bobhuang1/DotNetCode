# Client Samples

Sample HTTP clients that call the [`AzureFunction`](../AzureFunction) email relay
endpoints (`/Office365SmtpSimple` and `/Office365SmtpRelay`). Two versions are
provided because the idiomatic way to create and manage an `HttpClient` differs
between .NET Framework and modern .NET:

| Sample | Target | Notable differences from the other sample |
|---|---|---|
| [`NetFramework48/`](NetFramework48) | .NET Framework 4.8 | A single static `HttpClient` reused for the app's lifetime; `ServicePointManager.SecurityProtocol` set explicitly to TLS 1.2; `Newtonsoft.Json`; classic `static async Task<int> Main()` |
| [`Net8Plus/`](Net8Plus) | .NET 8 (also builds against .NET 6 / .NET 10) | `HttpClient` obtained from `IHttpClientFactory` via the generic host's DI container (`AddHttpClient<T>`); `System.Text.Json`; top-level statements |

Both samples call the same two endpoints and print the HTTP status + response body.

## Configuration

Both samples read the target function from environment variables:

| Variable | Purpose |
|---|---|
| `FUNCTION_BASE_URL` | Base URL of the deployed (or locally running) function, e.g. `https://your-function-app.azurewebsites.net` or `http://localhost:7071` |
| `FUNCTION_KEY` | The function key (see Azure Portal → Function App → App keys, or the `func start` console output for local runs) |

If unset, the samples fall back to placeholder values so the project still builds
and runs (it will just fail the HTTP call with a 401/404 until you point it at a
real function).

## Running

```bash
# .NET Framework 4.8 sample
cd NetFramework48
dotnet run

# .NET 8+ sample
cd Net8Plus
dotnet run
```
