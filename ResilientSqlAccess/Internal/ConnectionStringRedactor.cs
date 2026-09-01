using System.Text.RegularExpressions;

namespace ResilientSqlAccess.Internal
{
    /// <summary>Strips secret values out of a connection string before it is logged.</summary>
    internal static class ConnectionStringRedactor
    {
        private static readonly Regex SecretPattern = new(
            @"(?i)\b(password|pwd|clientsecret)\s*=\s*[^;]*;?",
            RegexOptions.Compiled);

        public static string Redact(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return string.Empty;
            return SecretPattern.Replace(connectionString, "$1=***;");
        }
    }
}
