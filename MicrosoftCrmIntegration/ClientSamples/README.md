# ClientSamples

Sample HTTP client code that calls the [`AzureFunction`](../AzureFunction)'s
generic CRUD endpoints (`GET`/`POST`/`PATCH`/`DELETE` on
`/dataverse/{entitySet}[/{id}]`). Two separate samples are provided because
the idiomatic way to create and use an `HttpClient` differs between the two
platforms:

| Sample | Target | `HttpClient` comes from |
|---|---|---|
| [`NetFramework48`](NetFramework48) | .NET Framework 4.7/4.8 | A hand-rolled `static readonly HttpClient`, with TLS 1.2 set explicitly via `ServicePointManager` |
| [`Net8Plus`](Net8Plus) | .NET 6/8/10 | `IHttpClientFactory` via `AddHttpClient<T>()` on the generic host's DI container |

Both samples call the same two example operations (list accounts, create a
contact) - only the client plumbing differs. See the comment block at the
top of each `Program.cs` for the full list of differences.

## Running a sample

```
cd NetFramework48 && dotnet run
cd Net8Plus && dotnet run
```

Set these environment variables first (both samples fall back to placeholder
values otherwise, which will fail against a real deployment):

```
set FUNCTION_BASE_URL=https://your-function-app.azurewebsites.net
set FUNCTION_KEY=your-function-key
```

Or, to test against a local `func start` instance, set
`FUNCTION_BASE_URL=http://localhost:7071` and leave `FUNCTION_KEY` unset (no
key is required for local requests).
