using System.Text;
using Microsoft.Data.SqlClient;

namespace ResilientSqlAccess.Internal
{
    internal static class SqlExceptionFormatter
    {
        public static string Format(string commandText, SqlException ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SQL error executing \"{commandText}\":");

            for (var i = 0; i < ex.Errors.Count; i++)
            {
                var err = ex.Errors[i];
                sb.AppendLine(
                    $"  [{i + 1}/{ex.Errors.Count}] Number={err.Number} Line={err.LineNumber} " +
                    $"Procedure=\"{err.Procedure}\" Message={err.Message}");
            }

            return sb.ToString();
        }
    }
}
