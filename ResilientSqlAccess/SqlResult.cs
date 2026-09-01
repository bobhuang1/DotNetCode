using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace ResilientSqlAccess
{
    /// <summary>
    /// Outcome of a <see cref="ResilientSqlClient"/> call: the raw result plus, for stored
    /// procedures, any output parameters and the procedure's RETURN value.
    /// </summary>
    public sealed class SqlResult
    {
        /// <summary>
        /// Raw result of the call: an <see cref="int"/> for ExecuteScalar/ExecuteNonQuery-style
        /// calls, or a <see cref="DataSet"/> for query calls. Use the AsXxx() helpers below, or
        /// cast it yourself, to consume it in a strongly typed way.
        /// </summary>
        public object? Result { get; set; }

        /// <summary>Empty when <see cref="IsCriticalError"/> is false; otherwise a diagnostic message safe to log.</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>True if the call failed after exhausting all configured retries (or hit a non-retryable error).</summary>
        public bool IsCriticalError { get; set; }

        /// <summary>True when the call completed without error. Equivalent to <c>!IsCriticalError</c>.</summary>
        public bool Succeeded => !IsCriticalError;

        /// <summary>
        /// For stored procedure calls: every Output/InputOutput parameter's final value, keyed by
        /// parameter name (including the leading '@'). Empty for plain SQL text commands.
        /// </summary>
        public Dictionary<string, object?> OutputParameters { get; } = new();

        /// <summary>
        /// For stored procedure calls: the procedure's RETURN value (an int - RETURN 0 means
        /// success by SQL Server convention, though your procedure may define its own meaning).
        /// Null for plain SQL text commands.
        /// </summary>
        public int? ReturnValue { get; set; }

        public int AsInt32() => Result switch
        {
            null or DBNull => 0,
            int i => i,
            _ => Convert.ToInt32(Result, CultureInfo.InvariantCulture)
        };

        public long AsInt64() => Result switch
        {
            null or DBNull => 0L,
            long l => l,
            _ => Convert.ToInt64(Result, CultureInfo.InvariantCulture)
        };

        public decimal AsDecimal() => Result switch
        {
            null or DBNull => 0m,
            decimal d => d,
            _ => Convert.ToDecimal(Result, CultureInfo.InvariantCulture)
        };

        public bool AsBoolean() => Result switch
        {
            null or DBNull => false,
            bool b => b,
            _ => Convert.ToBoolean(Result, CultureInfo.InvariantCulture)
        };

        public string AsString() => Result is null or DBNull ? string.Empty : Convert.ToString(Result, CultureInfo.InvariantCulture) ?? string.Empty;

        public DateTime AsDateTime() => Result switch
        {
            null or DBNull => DateTime.MinValue,
            DateTime dt => dt,
            _ => Convert.ToDateTime(Result, CultureInfo.InvariantCulture)
        };

        /// <summary>The first result set as a <see cref="DataTable"/>, or an empty table if there is none.</summary>
        public DataTable AsDataTable() =>
            Result is DataSet { Tables.Count: > 0 } ds ? ds.Tables[0] : new DataTable();

        /// <summary>All result sets. Only populated by ExecuteQueryAsync.</summary>
        public DataSet AsDataSet() => Result as DataSet ?? new DataSet();

        /// <summary>True if the query returned no rows (or wasn't a query at all).</summary>
        public bool IsEmpty => AsDataTable().Rows.Count == 0;

        /// <summary>Reads an output parameter by name (with or without the leading '@'), converted with <see cref="Convert.ChangeType(object, Type)"/>.</summary>
        public T? GetOutputParameter<T>(string parameterName)
        {
            var key = parameterName.StartsWith("@", StringComparison.Ordinal) ? parameterName : "@" + parameterName;
            if (!OutputParameters.TryGetValue(key, out var value) || value is null or DBNull)
                return default;

            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
    }
}
