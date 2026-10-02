using System;
using System.Text.RegularExpressions;

namespace McpServerLibrary.Services
{
    /// <summary>
    /// Builds the relative downstream path for a tool call from model-supplied arguments.
    /// <para>
    /// The arguments come from the language model, so they are untrusted: a prompt injection
    /// can make the model pass an absolute URL or a "../" segment. Only a plain collection
    /// name is accepted as the resource type, and the id is percent-encoded into a single
    /// path segment, so a call can never leave the configured base URL.
    /// </para>
    /// </summary>
    public static class ResourcePath
    {
        private static readonly Regex ResourceTypePattern =
            new Regex("^[A-Za-z][A-Za-z0-9_-]{0,63}$", RegexOptions.CultureInvariant);

        /// <summary>Returns "{resourceType}/{escaped resourceId}" or throws <see cref="ArgumentException"/>.</summary>
        public static string Build(string resourceType, string resourceId)
        {
            if (resourceType is null || !ResourceTypePattern.IsMatch(resourceType))
            {
                throw new ArgumentException(
                    "resourceType must be a plain collection name such as \"orders\" (letters, digits, '-' or '_').",
                    nameof(resourceType));
            }

            if (string.IsNullOrWhiteSpace(resourceId) || resourceId.Length > 200)
            {
                throw new ArgumentException("resourceId must be a non-empty identifier of at most 200 characters.", nameof(resourceId));
            }

            return resourceType + "/" + Uri.EscapeDataString(resourceId);
        }
    }
}
