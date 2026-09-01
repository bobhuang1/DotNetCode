using System;
using System.Data;
using System.Text;

namespace ResilientSqlAccess.Internal
{
    /// <summary>
    /// Distinguishes a bare stored-procedure name (e.g. "usp_GetCustomer") from free-form SQL
    /// text, so callers can pass either without having to specify CommandType themselves.
    /// </summary>
    internal static class SqlCommandTypeDetector
    {
        private static readonly string[] SqlKeywords =
        {
            "SELECT", "UPDATE", "INSERT", "DELETE", "MERGE", "EXEC", "EXECUTE", "WITH", "DECLARE"
        };

        /// <summary>
        /// Stored-procedure names cannot contain whitespace, so any whitespace means Text.
        /// Leading comments are stripped first, and keyword matches use a word boundary so a
        /// procedure literally named "SelectProducts" is not misclassified as SQL text.
        /// </summary>
        public static CommandType Detect(string commandText)
        {
            var withoutComments = StripComments(commandText).Trim();

            if (string.IsNullOrWhiteSpace(withoutComments)) return CommandType.Text;

            var upper = withoutComments.ToUpperInvariant();
            if (upper.IndexOf(' ') >= 0) return CommandType.Text;

            foreach (var keyword in SqlKeywords)
            {
                var followedByBoundary = upper.Length == keyword.Length || !char.IsLetterOrDigit(upper[keyword.Length]);
                if (upper.StartsWith(keyword, StringComparison.Ordinal) && followedByBoundary)
                    return CommandType.Text;
            }

            return CommandType.StoredProcedure;
        }

        /// <summary>Removes T-SQL line (--) and block (/* */) comments.</summary>
        private static string StripComments(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder(input.Length);
            var i = 0;

            while (i < input.Length)
            {
                if (input[i] == '-' && i + 1 < input.Length && input[i + 1] == '-')
                {
                    while (i < input.Length && input[i] != '\n') i++;
                }
                else if (input[i] == '/' && i + 1 < input.Length && input[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < input.Length && !(input[i] == '*' && input[i + 1] == '/')) i++;
                    i += 2;
                    if (i > input.Length) i = input.Length;
                }
                else
                {
                    sb.Append(input[i]);
                    i++;
                }
            }

            return sb.ToString();
        }
    }
}
