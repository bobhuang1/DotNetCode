namespace IdentityContext
{
    /// <summary>
    /// A minimal, framework-agnostic view of "the current web request", so
    /// <see cref="DomainIdentity"/> can work the same way whether the caller is a classic
    /// ASP.NET (System.Web) app or an ASP.NET Core app - the two have completely different
    /// HttpContext types with no common base, so this library defines its own tiny seam
    /// instead of depending on either. See README.md for adapter implementations of both.
    /// </summary>
    public interface IWebRequestContext
    {
        /// <summary>The authenticated user's identity name (e.g. "DOMAIN\username"), if any.</summary>
        string? UserIdentityName { get; }

        /// <summary>Raw value of the X-Forwarded-For header, if present.</summary>
        string? ForwardedForHeader { get; }

        /// <summary>The direct TCP peer address (before considering proxies).</summary>
        string? RemoteAddress { get; }

        /// <summary>Request scheme, e.g. "https".</summary>
        string? Scheme { get; }

        /// <summary>Host plus port, e.g. "example.com" or "example.com:8443".</summary>
        string? Authority { get; }

        /// <summary>The application's base path within the host, e.g. "/" or "/myapp".</summary>
        string? ApplicationPath { get; }
    }
}
