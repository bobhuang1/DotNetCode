using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;

namespace IdentityContext
{
    /// <summary>
    /// Helpers for the current Windows/domain identity and (via <see cref="IWebRequestContext"/>)
    /// the current web request's user, client IP, and site URL. See README.md for what was
    /// changed or removed versus the internal utility this is based on.
    /// </summary>
    public static class DomainIdentity
    {
        /// <summary>
        /// Optional domain-name-to-email-suffix mapping (e.g. <c>"CORP"</c> -&gt;
        /// <c>"@corp.example.com"</c>), consulted by <see cref="GetCurrentUserDomainEmail"/>.
        /// Empty by default - populate it with your own organization's domains at startup.
        /// </summary>
        public static IDictionary<string, string> DomainToEmailSuffixMap { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional hook to override the resolved username for specific domains (e.g. a shared
        /// build-agent or kiosk account that should always map to a specific mailbox), consulted
        /// by <see cref="GetCurrentUserDomainEmail"/> before applying <see cref="DomainToEmailSuffixMap"/>.
        /// Return null/empty to fall through to the logon's own username.
        /// </summary>
        public static Func<string, string?>? UserNameOverrideResolver { get; set; }

        /// <summary>True when a debugger is attached - a common (imperfect) proxy for "running on a developer's machine".</summary>
        public static bool IsDevMachine() => Debugger.IsAttached;

        /// <summary>
        /// The current Windows identity's logon name ("DOMAIN\username"), or empty string if
        /// unavailable. Windows-only: returns empty string on non-Windows OSes rather than throwing.
        /// </summary>
        public static string GetCurrentWindowsLogonName()
        {
            try
            {
#if NET6_0_OR_GREATER
                if (!OperatingSystem.IsWindows()) return string.Empty;
#endif
                return (WindowsIdentity.GetCurrent()?.Name ?? string.Empty).Trim();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// The current user's logon name, lowercased and trimmed: prefers the web request's
        /// authenticated identity (if <paramref name="webContext"/> is supplied and has one),
        /// falling back to the current Windows identity.
        /// </summary>
        public static string GetCurrentUserLogonName(IWebRequestContext? webContext = null)
        {
            var name = webContext?.UserIdentityName;
            if (string.IsNullOrWhiteSpace(name)) name = GetCurrentWindowsLogonName();
            return (name ?? string.Empty).Trim().ToLowerInvariant();
        }

        /// <summary>Replaces backslashes with forward slashes, e.g. for embedding a logon name in a URL path segment.</summary>
        public static string EscapeLogonForUrl(string logonName) => (logonName ?? string.Empty).Replace('\\', '/');

        /// <summary>Splits "DOMAIN\username" into its two parts. If there's no backslash, Domain is empty and UserName is the whole input.</summary>
        public static (string Domain, string UserName) SplitDomainLogon(string logonName)
        {
            if (string.IsNullOrEmpty(logonName)) return (string.Empty, string.Empty);

            var separatorIndex = logonName.IndexOf('\\');
            return separatorIndex < 0
                ? (string.Empty, logonName)
                : (logonName.Substring(0, separatorIndex), logonName.Substring(separatorIndex + 1));
        }

        /// <summary>
        /// Best-effort email address for the current user, derived from their domain logon name.
        /// Looks up the domain in <see cref="DomainToEmailSuffixMap"/>; if not found, guesses
        /// <c>@{domain}.com</c> (or just <c>@{domain}</c> if it already looks like a hostname).
        /// Returns empty string if no logon name is available. This is inherently
        /// environment-specific - populate <see cref="DomainToEmailSuffixMap"/> (and optionally
        /// <see cref="UserNameOverrideResolver"/>) for your own organization before relying on it.
        /// </summary>
        public static string GetCurrentUserDomainEmail(IWebRequestContext? webContext = null)
        {
            var logon = GetCurrentUserLogonName(webContext);
            if (string.IsNullOrEmpty(logon)) return string.Empty;

            var (domain, userName) = SplitDomainLogon(logon);
            if (string.IsNullOrEmpty(domain)) return logon; // no domain part - logon name is already the best answer

            var overriddenUserName = UserNameOverrideResolver?.Invoke(domain);
            if (!string.IsNullOrEmpty(overriddenUserName)) userName = overriddenUserName!;

            if (DomainToEmailSuffixMap.TryGetValue(domain, out var suffix)) return userName + suffix;

            var guessedSuffix = "@" + (domain.Contains(".") ? domain : domain + ".com");
            return userName + guessedSuffix;
        }

        /// <summary>The requesting client's IP address, preferring X-Forwarded-For (first entry) over the direct TCP peer address.</summary>
        public static string GetClientIpAddress(IWebRequestContext webContext)
        {
            if (webContext == null) throw new ArgumentNullException(nameof(webContext));

            var forwarded = webContext.ForwardedForHeader;
            if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded!.Split(',')[0].Trim();

            return webContext.RemoteAddress ?? string.Empty;
        }

        /// <summary>The current site's base URL, e.g. "https://example.com/myapp/". Empty string if scheme/authority are unavailable.</summary>
        public static string GetSiteBaseUrl(IWebRequestContext webContext)
        {
            if (webContext == null) throw new ArgumentNullException(nameof(webContext));
            if (string.IsNullOrEmpty(webContext.Scheme) || string.IsNullOrEmpty(webContext.Authority)) return string.Empty;

            var appPath = (webContext.ApplicationPath ?? string.Empty).TrimEnd('/');
            return $"{webContext.Scheme}://{webContext.Authority}{appPath}/";
        }
    }
}
