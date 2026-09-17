# WebApi

ASP.NET Core (.NET 10) host that exposes the shared
[`GenericApiTools`](../McpServerLibrary/Tools/GenericApiTools.cs) over the
**Streamable HTTP** transport at `/mcp`, using
`ModelContextProtocol.AspNetCore`.

| Endpoint | Method(s) | Purpose |
|---|---|---|
| `/mcp` | `POST` (and `GET`/`DELETE` for sessions) | MCP Streamable HTTP endpoint |

## Run it

```bash
dotnet run
```

By default it listens on the ASP.NET Core URLs from your launch profile /
environment (for example `ASPNETCORE_URLS=http://127.0.0.1:5199`).

With `KeyVault:VaultUrl` left **empty**, the API-key gate is skipped so you can
try the endpoint without a vault:

```bash
curl -s -X POST http://127.0.0.1:5199/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"1.0"}}}'
```

You get back an `initialize` result naming `McpWebApi` and its capabilities.
Point an MCP client (MCP Inspector, VS Code + Copilot, etc.) at
`http://127.0.0.1:5199/mcp` with the **Streamable HTTP** transport to list and
call the tools.

## Configuration

`appsettings.json` holds the placeholders; `appsettings.Development.json` is
where you put your real vault name for local runs.

| Setting | What it is |
|---|---|
| `KeyVault:VaultUrl` | Key Vault URI. Empty disables the API-key gate (local-only convenience) |
| `KeyVault:DownstreamApiBaseUrl` | Base URL of the generic downstream API the tools call |
| `KeyVault:UserNameSecretName` | Key Vault secret holding the downstream API user name |
| `KeyVault:ApiSecretSecretName` | Key Vault secret holding the downstream API key/password |
| `KeyVault:McpClientApiKeySecretName` | Key Vault secret whose value callers must send as the `x-api-key` header |

## Authentication

A small middleware gates `/mcp`: it reads the `mcp-client-api-key` secret from
Key Vault and compares it to the incoming `x-api-key` header, returning `401`
on a mismatch. That's deliberately minimal - swap it for the auth that matches
your environment (Entra ID, API Management, App Service Authentication) in
production. The pattern to keep is the same: **the expected value comes from
Key Vault, never from the config file or source.**

## Security note

Placeholders only - replace the vault name, secret names and downstream URL
before running for real, and never commit real secrets.
