# NetFramework48

Console host targeting **.NET Framework 4.8** that runs the shared
[`GenericApiTools`](../McpServerLibrary/Tools/GenericApiTools.cs) over the
**stdio** transport. An MCP client launches this executable as a local
process and speaks JSON-RPC over stdin/stdout - no HTTP listener, no hosted
web server, no .NET 10 requirement on the host machine.

## Why this exists

Plenty of Windows line-of-business apps are still on .NET Framework, and a
stdio MCP server is the natural way to expose their capabilities to a
locally-running agent: the client owns the process lifecycle, there's nothing
to expose on the network, and the shared library's `netstandard2.0`
consumption path does the heavy lifting.

## Run it

```bash
dotnet build
./bin/Debug/net48/McpNetFramework48.StdioHost.exe
```

It reads JSON-RPC from stdin until stdin is closed. Typical MCP client
configuration (VS Code, Claude Desktop, etc.):

```json
{
  "mcpServers": {
    "mcp-netframework48": {
      "command": "C:\\path\\to\\McpNetFramework48.StdioHost.exe",
      "env": {
        "MCP_VAULT_URL": "https://<your-vault-name>.vault.azure.net/",
        "MCP_DOWNSTREAM_API_BASE_URL": "https://api.example.com/v1"
      }
    }
  }
}
```

## Configuration (environment variables)

| Variable | What it is |
|---|---|
| `MCP_VAULT_URL` | Key Vault URI |
| `MCP_DOWNSTREAM_API_BASE_URL` | Base URL of the generic downstream API |

Secret *names* fall back to the defaults in `KeyVaultSettings`
(`mcp-sample-user`, `mcp-sample-api-key`).

## Implementation notes

- **Logs go to stderr.** stdout is the protocol channel; the host configures
  `ConsoleLoggerOptions.LogToStandardErrorThreshold` so console logging can't
  corrupt the JSON-RPC stream.
- **The service provider is disposed asynchronously** (`await using`), because
  `McpServer` only implements `IAsyncDisposable` - synchronously disposing the
  provider throws.
- The MCP SDK is consumed through its `netstandard2.0` asset, which is why
  this host can stay on .NET Framework 4.8.

## Security note

Placeholders only - replace the vault URL and downstream API URL before
running for real, and never commit real secrets.
