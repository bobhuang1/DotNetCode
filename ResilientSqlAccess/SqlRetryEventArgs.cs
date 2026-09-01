using System;
using Microsoft.Data.SqlClient;

namespace ResilientSqlAccess
{
    /// <summary>Raised via <see cref="ResilientSqlClient.Retrying"/> just before a retry attempt sleeps.</summary>
    public sealed class SqlRetryEventArgs : EventArgs
    {
        public SqlRetryEventArgs(string operationName, string commandText, int attempt, int maxAttempts, TimeSpan delay, SqlException exception)
        {
            OperationName = operationName;
            CommandText = commandText;
            Attempt = attempt;
            MaxAttempts = maxAttempts;
            Delay = delay;
            Exception = exception;
        }

        /// <summary>Which client method was called: ExecuteScalarAsync, ExecuteQueryAsync, or ExecuteNonQueryAsync.</summary>
        public string OperationName { get; }

        /// <summary>The SQL text or stored procedure name being executed.</summary>
        public string CommandText { get; }

        /// <summary>1-based attempt number that just failed.</summary>
        public int Attempt { get; }

        /// <summary>Configured <see cref="SqlRetryOptions.RetryCount"/>.</summary>
        public int MaxAttempts { get; }

        /// <summary>How long the client will wait before the next attempt.</summary>
        public TimeSpan Delay { get; }

        /// <summary>The exception that triggered this retry.</summary>
        public SqlException Exception { get; }
    }
}
