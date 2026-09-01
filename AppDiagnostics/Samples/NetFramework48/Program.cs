using System;
using AppDiagnostics;
using Microsoft.Extensions.Logging;

namespace ConsoleSample.NetFramework48
{
    internal static class Program
    {
        private static void Main()
        {
            // Both outputs are opt-in and off by default.
            AppDiagnostics.AppDiagnostics.EnableDebugOutput = true; // visible via a debugger / DebugView, not this console
            AppDiagnostics.AppDiagnostics.EnableLogOutput = true;
            AppDiagnostics.AppDiagnostics.Logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Sample");

            AppDiagnostics.AppDiagnostics.Log(nameof(Main), "Application starting.");

            try
            {
                throw new InvalidOperationException("Simulated failure for the sample.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(AppDiagnostics.AppDiagnostics.GetStandardException(ex));
                AppDiagnostics.AppDiagnostics.Log(nameof(Main), ex, isError: true);
            }

            var timeoutMessage = AppDiagnostics.AppDiagnostics.GetTimeoutException(new TimeoutException("Simulated timeout."));
            Console.WriteLine(timeoutMessage);

            var userSuppliedText = "</script><script>alert('xss')</script>";
            Console.WriteLine($"EscapeJsString(...) = {AppDiagnostics.AppDiagnostics.EscapeJsString(userSuppliedText)}");
        }
    }
}
