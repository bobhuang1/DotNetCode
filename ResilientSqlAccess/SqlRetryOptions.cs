using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace ResilientSqlAccess
{
    /// <summary>
    /// Controls how <see cref="ResilientSqlClient"/> retries a failed SQL operation.
    /// </summary>
    public sealed class SqlRetryOptions
    {
        /// <summary>Maximum number of retry attempts after the initial try. Default: 3.</summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>Base delay used by the backoff calculation. Default: 1 second.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>How the delay grows between attempts. Default: Exponential.</summary>
        public SqlRetryBackoffType BackoffType { get; set; } = SqlRetryBackoffType.Exponential;

        /// <summary>
        /// SQL Server / Azure SQL error numbers that are retried because they are known to be
        /// transient (connection drops, failover, throttling, deadlock). Every other
        /// <see cref="SqlException"/> - syntax errors, permission errors, constraint violations,
        /// conversion errors, bad credentials - fails immediately, since retrying it cannot
        /// succeed and only delays the error. Add numbers here for transient errors specific
        /// to your environment. See README.md.
        /// </summary>
        public ISet<int> TransientErrorNumbers { get; set; } = new HashSet<int>
        {
            -2,    // Command timeout (see RetryNonQueryOnTimeout for writes)
            -1,    // Generic client-side connection failure
            2,     // Network-related or instance-specific error establishing the connection
            53,    // Server not found / not accessible
            64,    // Connection forcibly closed during login
            121,   // Semaphore timeout (transport-level error)
            233,   // No process on the other end of the pipe (connection reset pre-login)
            1205,  // Deadlock victim
            4060,  // Cannot open database (often during failover)
            4221,  // Login to read-secondary failed due to long wait on HADR_DATABASE_WAIT_FOR_TRANSITION_TO_VERSIONING
            10053, // Transport-level error: connection aborted
            10054, // Transport-level error: connection reset by peer
            10060, // Network-related timeout
            10928, // Azure SQL resource limit reached
            10929, // Azure SQL resource limit reached (min guarantee)
            40143, // Azure SQL: connection could not be initialized
            40197, // Azure SQL: service error processing the request (failover)
            40501, // Azure SQL: service is busy
            40540, // Azure SQL: service encountered an error
            40613, // Azure SQL: database not currently available
            49918, // Azure SQL: not enough resources to process the request
            49919, // Azure SQL: too many create/update operations
            49920  // Azure SQL: too many operations in progress
        };

        /// <summary>
        /// Whether a command timeout (error -2) on <c>ExecuteNonQueryAsync</c> is retried.
        /// Default: false. A timed-out INSERT/UPDATE/DELETE may already have committed on the
        /// server, so retrying it can apply the change twice. Set to true only for statements
        /// that are idempotent (or guarded by a key the database enforces).
        /// </summary>
        public bool RetryNonQueryOnTimeout { get; set; }

        /// <summary>
        /// Optional override for deciding whether a given <see cref="SqlException"/> should be
        /// retried. When set, this takes priority over <see cref="TransientErrorNumbers"/> and
        /// <see cref="RetryNonQueryOnTimeout"/>.
        /// </summary>
        public Func<SqlException, bool>? IsTransient { get; set; }

        /// <summary>
        /// Size (in bytes/chars) assigned to output/input-output parameters that don't already
        /// specify one - SqlParameter requires a size for most output parameter types. Default: 4000.
        /// </summary>
        public int OutputParameterDefaultSize { get; set; } = 4000;

        /// <summary>Command timeout, in seconds, applied to every command. Null uses the ADO.NET default (30s).</summary>
        public int? CommandTimeoutSeconds { get; set; }

        /// <summary>Computes the delay before the given attempt (1-based) using <see cref="BackoffType"/>.</summary>
        public TimeSpan GetDelayForAttempt(int attempt)
        {
            return BackoffType switch
            {
                SqlRetryBackoffType.Constant => RetryDelay,
                SqlRetryBackoffType.Linear => TimeSpan.FromMilliseconds(RetryDelay.TotalMilliseconds * attempt),
                SqlRetryBackoffType.Exponential => TimeSpan.FromMilliseconds(RetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)),
                _ => RetryDelay
            };
        }
    }
}
