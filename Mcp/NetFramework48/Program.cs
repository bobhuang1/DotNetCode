using System;
using System.Threading.Tasks;
using McpServerLibrary;
using McpServerLibrary.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mcp.NetFramework48
{
    /// <summary>
    /// Runs the shared MCP tools as a local stdio server on .NET Framework 4.8,
    /// launched directly by an MCP client over stdin/stdout.
    /// </summary>
    internal static class Program
    {
        private static async Task Main()
        {
            var services = new ServiceCollection();

            // stdout carries the JSON-RPC protocol, so console logging must go to stderr instead.
            services.AddLogging(logging =>
            {
                logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
            });

            services.AddMcpServerLibrary(settings =>
            {
                settings.VaultUrl = Environment.GetEnvironmentVariable("MCP_VAULT_URL") ?? string.Empty;
                settings.DownstreamApiBaseUrl = Environment.GetEnvironmentVariable("MCP_DOWNSTREAM_API_BASE_URL") ?? string.Empty;
            });

            services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<GenericApiTools>();

            // McpServer only implements IAsyncDisposable, so the provider must be disposed asynchronously.
            await using var provider = services.BuildServiceProvider();
            var server = provider.GetRequiredService<McpServer>();

            await server.RunAsync().ConfigureAwait(false);
        }
    }
}