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
        /// SQL Server error numbers that should NOT be retried, because retrying them can't
        /// possibly succeed (e.g. bad credentials). Every other <see cref="SqlException"/> is
        /// treated as transient and retried - this is a deliberate "retry by default, opt out
        /// specific errors" design rather than an allow-list of specific transient error codes,
        /// so an unfamiliar transient error you haven't seen before still gets retried.
        /// See README.md for guidance on tuning this list.
        /// </summary>
        public ISet<int> NonRetryableErrorNumbers { get; set; } = new HashSet<int>
        {
            -1,    // Generic client-side connection failure
            233,   // No process is on the other end of the pipe (connection reset pre-login)
            18456, // Login failed for user
            2      // A network-related or instance-specific error establishing the connection
        };

        /// <summary>
        /// Optional override for deciding whether a given <see cref="SqlException"/> should be
        /// retried. When set, this takes priority over <see cref="NonRetryableErrorNumbers"/>.
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
