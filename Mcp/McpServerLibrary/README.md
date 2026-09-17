# McpServerLibrary

The shared piece of the [Mcp](../README.md) samples: the tools themselves,
plus the credential plumbing they need. Reference this from any host -
ASP.NET Core, Azure Functions (isolated worker), or a plain console app - and
you get the same tools in all of them.

Targets `net48;net8.0;net10.0` from a single codebase. The `netstandard2.0`
asset of the MCP SDK is what lets the .NET Framework 4.8 host consume it.

| Folder | What it is |
|---|---|
| [`Configuration/`](Configuration) | `GenericCredentials` (user name / API key / base URL) and `KeyVaultSettings` (vault URL and secret names) |
| [`KeyVault/`](KeyVault) | `IKeyVaultCredentialProvider` - resolves credentials and individual secrets from Key Vault using `DefaultAzureCredential` |
| [`Services/`](Services) | `IGenericApiClient` - minimal Basic-auth `GET`/`POST` client for the downstream REST API |
| [`Tools/`](Tools) | `GenericApiTools` - the `[McpServerTool]` methods (`get_status`, `send_update`) exposed by every host |

## Wiring it up

```csharp
// One line registers both the Key Vault credential provider and the HTTP client.
services.AddMcpServerLibrary(settings =>
{
    settings.VaultUrl = "https://<your-vault>.vault.azure.net/";
    settings.DownstreamApiBaseUrl = "https://api.example.com/v1";
});

// Then let the MCP SDK discover the tools (ASP.NET Core / console hosts).
services.AddMcpServer()
    .WithHttpTransport()        // or .WithStdioServerTransport()
    .WithTools<GenericApiTools>();
```

There's also an overload that binds a configuration section instead, used by
the WebApi host:

```csharp
services.AddMcpServerLibrary(configuration); // reads the "KeyVault" section
```

## The tools

Both tools resolve their credentials from Key Vault on every call and send
them to `GenericCredentials.BaseUrl` as HTTP Basic auth, so the names
(`get_status`, `send_update`) and shapes are placeholders you can retarget to
your own downstream API.

| Tool | Arguments | Does |
|---|---|---|
| `get_status` | `resourceType`, `resourceId` | `GET {baseUrl}/{resourceType}/{resourceId}` |
| `send_update` | `resourceType`, `resourceId`, `jsonBody` | `POST {baseUrl}/{resourceType}/{resourceId}` |

Documentation on each method (`[Description]`) is what the AI model sees when
deciding whether to call the tool, so keep it descriptive when you add your
own.
