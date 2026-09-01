using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AppDiagnostics
{
    /// <summary>
    /// A small, static facade over debug output and an optional <see cref="ILogger"/>, plus
    /// exception-formatting and JS-string-escaping helpers. See README.md for scope and usage.
    /// </summary>
    public static class AppDiagnostics
    {
        /// <summary>When true, <see cref="Log"/> also writes to <see cref="System.Diagnostics.Debug"/>.</summary>
        public static bool EnableDebugOutput { get; set; }

        /// <summary>When true (and <see cref="Logger"/> is set), <see cref="Log"/> also writes to <see cref="Logger"/>.</summary>
        public static bool EnableLogOutput { get; set; }

        /// <summary>The logger <see cref="Log"/> writes to when <see cref="EnableLogOutput"/> is true. Not set by default.</summary>
        public static ILogger? Logger { get; set; }

        #region Exception formatting

        private static string FormatExceptionBase(Exception ex) =>
            $"Error Message: {ex.Message}. Source: {ex.Source}. Stack Trace: {ex.StackTrace}. Data: {ex.Data}. ";

        /// <summary>A single-line-friendly rendering of any exception, suitable for logging.</summary>
        public static string GetStandardException(Exception ex) => $"{FormatExceptionBase(ex)}{Environment.NewLine}";

        /// <summary>Same as <see cref="GetStandardException"/>, kept as a distinctly named overload for timeout-specific call sites.</summary>
        public static string GetTimeoutException(TimeoutException ex) => $"{FormatExceptionBase(ex)}{Environment.NewLine}";

        #endregion

        #region Logging / debug output

        /// <summary>
        /// Unified logging/debug output: writes to <see cref="System.Diagnostics.Debug"/> when
        /// <see cref="EnableDebugOutput"/> is set, and to <see cref="Logger"/> when
        /// <see cref="EnableLogOutput"/> is set and a logger has been assigned. Both are opt-in and
        /// off by default, so referencing this library doesn't produce any output on its own.
        /// </summary>
        public static void Log(string source, object message, bool isError = false)
        {
            var text = message is Exception ex ? GetStandardException(ex) : message?.ToString() ?? string.Empty;

            if (EnableDebugOutput)
            {
                Debug.WriteLine(isError ? $"[{source}] ERROR: {text}" : $"[{source}] INFO: {text}");
            }

            if (EnableLogOutput && Logger != null)
            {
                if (isError) Logger.LogError("[{Source}] {Message}", source, text);
                else Logger.LogInformation("[{Source}] {Message}", source, text);
            }
        }

        #endregion

        #region Text escaping

        /// <summary>
        /// Escapes a string for safe embedding inside a JS string literal (e.g. inside a
        /// dynamically generated <c>&lt;script&gt;</c> block) - escapes backslash/quotes/CR/LF and
        /// angle brackets, preventing both string-literal breakout and a literal
        /// <c>&lt;/script&gt;</c> from terminating the enclosing script block early.
        /// </summary>
        public static string EscapeJsString(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            return input!
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("<", "\\u003C")
                .Replace(">", "\\u003E");
        }

        #endregion
    }
}
