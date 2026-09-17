# AzureFunction

Isolated-worker Azure Function (.NET 10) that exposes the same capabilities as
the other [Mcp](../README.md) hosts, using the **Azure Functions MCP
extension** (`Microsoft.Azure.Functions.Worker.Extensions.Mcp`). The extension
turns `[McpToolTrigger]` functions into an MCP server - there's no `MapMcp`
call and no `[McpServerTool]` class here.

| Function | MCP tool | Arguments | Does |
|---|---|---|---|
| `GetStatus` | `get_status` | `resourceType`, `resourceId` | `GET {baseUrl}/{resourceType}/{resourceId}` on the downstream API |
| `SendUpdate` | `send_update` | `resourceType`, `resourceId`, `jsonBody` | `POST` the JSON payload to the same resource |

Both reuse `IGenericApiClient` / `IKeyVaultCredentialProvider` from
[`McpServerLibrary`](../McpServerLibrary) via constructor injection.

## Endpoints

| Transport | URL |
|---|---|
| Streamable HTTP | `http://localhost:7071/runtime/webhooks/mcp` |
| SSE (legacy) | `http://localhost:7071/runtime/webhooks/mcp/sse` |

`host.json` sets `extensions.mcp.system.webhookAuthorizationLevel` to
`Anonymous` so local clients can connect without a function key.

## Run it locally

1. Copy the sample settings and fill in your vault:

   ```bash
   cp local.settings.json.sample local.settings.json
   ```

2. Make sure your Azure identity is available (`az login`) - `DefaultAzureCredential` uses it.
3. Start the host:

   ```bash
   func start
   ```

4. Point an MCP client at `http://localhost:7071/runtime/webhooks/mcp` with the
   Streamable HTTP transport.

## Configuration (`local.settings.json` / app settings)

| Setting | What it is |
|---|---|
| `MCP_VAULT_URL` | Key Vault URI |
| `MCP_DOWNSTREAM_API_BASE_URL` | Base URL of the generic downstream API |
| `MCP_USERNAME_SECRET_NAME` | Secret holding the downstream API user name |
| `MCP_APISECRET_SECRET_NAME` | Secret holding the downstream API key/password |

`local.settings.json` is git-ignored; only `local.settings.json.sample` is
checked in.

## Version notes

- `Microsoft.Azure.Functions.Worker.Extensions.Mcp` 1.6.0 requires
  `Microsoft.Azure.Functions.Worker` 2.1.0+, `...Worker.Sdk` 2.0.2+, and
  Azure Functions Core Tools 4.0.7030+.
- The MCP extension supports the isolated worker model only - not the
  in-process model, and not .NET Framework.

## Security note

Placeholders only - replace the vault URL, secret names and downstream API
URL before running for real, and never commit real secrets.
