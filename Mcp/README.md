# Mcp

Sample code for exposing tools to AI agents over the **Model Context
Protocol** using the official [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)
(`ModelContextProtocol` 2.2.0).

One shared library defines the tools and the Key Vault-backed credentials.
Three hosts expose those same capabilities in the three shapes you actually
deploy: a .NET 10 ASP.NET Core Web API (remote HTTP), a .NET 10 Azure
Function (serverless remote HTTP), and a .NET Framework 4.8 console host
(local stdio) - so you can compare transports and hosting models without
rewriting the tool logic each time.

| Folder | What it is | When to use it |
|---|---|---|
| [`McpServerLibrary/`](McpServerLibrary) | Multi-targeted class library (`net48;net8.0;net10.0`): generic downstream API client, Azure Key Vault credential provider, and the shared `[McpServerTool]` methods | You want to define the tools once and host them from several app types |
| [`WebApi/`](WebApi) | ASP.NET Core (.NET 10) MCP server exposing the tools over the Streamable HTTP transport at `/mcp`, gated by an `x-api-key` secret read from Key Vault | You want a centrally deployed remote MCP server any HTTP-capable MCP client can reach |
| [`AzureFunction/`](AzureFunction) | Isolated-worker Azure Function (.NET 10) using the Azure Functions MCP extension (`[McpToolTrigger]`), served at `/runtime/webhooks/mcp` | You want a serverless remote MCP server that scales and deploys like any other function app |
| [`NetFramework48/`](NetFramework48) | Console host (.NET Framework 4.8) running the shared tools over the stdio transport | You need to expose tools from a legacy/on-premises Windows app that MCP clients launch as a local process |

## One set of tools, three hosts

- The WebApi and the .NET Framework 4.8 hosts add the shared tool class
  directly - `AddMcpServer().WithTools<GenericApiTools>()` - and differ only in
  the transport (`WithHttpTransport()` vs `WithStdioServerTransport()`).
- The Azure Function host uses the Functions MCP extension's own binding
  model (`[McpToolTrigger]` / `[McpToolProperty]`), which doesn't consume
  `[McpServerTool]` methods, but it reuses the exact same
  `IGenericApiClient` / `IKeyVaultCredentialProvider` services from the
  shared library.

## Credentials live in Azure Key Vault

None of the samples hold credentials. `KeyVaultCredentialProvider`
authenticates with `DefaultAzureCredential` (Azure CLI / Visual Studio sign-in
locally, managed identity in Azure) and reads the user name and API key used
to call the downstream REST API. The secrets are typically created like:

```bash
az keyvault secret set --vault-name <your-vault> --name mcp-sample-user     --value "svc-mcp-sample"
az keyvault secret set --vault-name <your-vault> --name mcp-sample-api-key  --value "<downstream-api-key>"
az keyvault secret set --vault-name <your-vault> --name mcp-client-api-key  --value "<key-callers-send-as-x-api-key>"
```

Locally, `DefaultAzureCredential` picks up whatever identity you're already
signed in as (`az login`, Visual Studio, VS Code, or
`AzureDeveloperCliCredential`).

## Which transport?

| Transport | Hosts | How the client connects | When to use |
|---|---|---|---|
| **Streamable HTTP** | `WebApi/`, `AzureFunction/` | Client POSTs JSON-RPC to an HTTP URL | Remote, shared, many clients, standard web auth/scaling |
| **stdio** | `NetFramework48/` | Client launches the process and speaks JSON-RPC over stdin/stdout | Local, single-user, no network listener, works from a legacy desktop app |

Over stdio, **stdout carries the protocol** - the .NET Framework host sends its
logs to stderr for exactly that reason, and so should any host you write.

## Framework targets

`McpServerLibrary` targets `net48;net8.0;net10.0`. `net6.0` is deliberately
**not** targeted here, unlike the other projects in this repository: the MCP
SDK 2.x dependency chain requires .NET 8 or later (it still offers a
`netstandard2.0` asset, which is how the .NET Framework host consumes it).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) - for the library, WebApi and Azure Function
- .NET Framework 4.8 - for the `NetFramework48` console host (builds with the .NET SDK on Windows)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) 4.0.7030 or later - for the Azure Function host
- An Azure Key Vault with the secrets above, plus a signed-in Azure identity for local runs

## Security note

This is sample/portfolio code. Every secret, vault name, and API URL here is a
placeholder - replace them with your own before running anything for real, and
never commit real secrets. Key Vault is used precisely so that credentials
live outside the repository.
