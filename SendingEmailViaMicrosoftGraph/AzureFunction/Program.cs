#nullable enable
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SendingEmailViaMicrosoftGraph.AzureFunction
{
    public class Program
    {
        public static void Main()
        {
            var debuggerAttached = Debugger.IsAttached;

            Console.WriteLine("====================================================");
            Console.WriteLine(" SENDING EMAIL VIA MICROSOFT GRAPH - HOST STARTING");
            Console.WriteLine("====================================================");
            Console.WriteLine($"Environment: {(debuggerAttached ? "LOCAL DEBUG" : "AZURE / HOSTED")}");
            Console.WriteLine("====================================================");

            // Application Insights / OpenTelemetry export is entirely optional for this
            // sample. If APPLICATIONINSIGHTS_CONNECTION_STRING is not configured, the
            // function still runs (and still sends email) with telemetry disabled.
            var aiConnectionString =
                Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

            var enableTelemetry = !string.IsNullOrWhiteSpace(aiConnectionString);

            if (!enableTelemetry)
            {
                Console.WriteLine("APPLICATIONINSIGHTS_CONNECTION_STRING not configured; telemetry is disabled.");
            }

            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureOpenApi()
                .ConfigureServices(services =>
                {
                    if (enableTelemetry)
                    {
                        services.AddOpenTelemetry()
                            .ConfigureResource(resource =>
                            {
                                resource.AddService(
                                    serviceName: "SendingEmailViaMicrosoftGraph.AzureFunction",
                                    serviceVersion: "1.0.0");
                            })
                            .WithMetrics(meterProviderBuilder =>
                            {
                                meterProviderBuilder
                                    .AddMeter("SendingEmailViaMicrosoftGraph.AzureFunction")
                                    .AddAzureMonitorMetricExporter(o =>
                                    {
                                        o.ConnectionString = aiConnectionString;
                                    });
                            })
                            .WithTracing(tracerProviderBuilder =>
                            {
                                tracerProviderBuilder
                                    .AddSource(EmailRelayFunctions.GraphActivitySourceName)
                                    .AddAzureMonitorTraceExporter(o =>
                                    {
                                        o.ConnectionString = aiConnectionString;
                                    });
                            });
                    }
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddFilter("Microsoft", LogLevel.Warning);
                    logging.AddFilter("System", LogLevel.Warning);
                    logging.AddFilter("Microsoft.Azure.Functions.Worker", LogLevel.Warning);

                    if (enableTelemetry)
                    {
                        logging.AddOpenTelemetry(options =>
                        {
                            options.IncludeScopes = true;

                            options.AddAzureMonitorLogExporter(o =>
                            {
                                o.ConnectionString = aiConnectionString;
                            });
                        });
                    }
                })
                .Build();

            host.Run();
        }
    }
}
