using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Polly;
using ResilientSqlAccess.Internal;

namespace ResilientSqlAccess
{
    /// <summary>
    /// Executes SQL commands (raw text or stored procedures) against SQL Server / Azure SQL
    /// with automatic retry-with-backoff on transient failures. See README.md for a full guide,
    /// including stored procedure and output-parameter usage.
    /// </summary>
    public sealed class ResilientSqlClient
    {
        private readonly string _connectionString;
        private readonly SqlRetryOptions _options;
        private readonly ILogger<ResilientSqlClient>? _logger;

        public ResilientSqlClient(string connectionString, SqlRetryOptions? options = null, ILogger<ResilientSqlClient>? logger = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("A connection string is required.", nameof(connectionString));

            _connectionString = connectionString;
            _options = options ?? new SqlRetryOptions();
            _logger = logger;
        }

        /// <summary>
        /// Raised just before each retry attempt sleeps. Useful when you don't have an
        /// <see cref="ILogger"/> wired up (e.g. a plain .NET Framework console app) and just
        /// want to print progress.
        /// </summary>
        public event EventHandler<SqlRetryEventArgs>? Retrying;

        // ============================================================
        // PUBLIC API
        // ============================================================

        /// <summary>
        /// Executes a command and returns a single scalar value (e.g. <c>SELECT COUNT(*)</c>, or
        /// a stored procedure's first selected column via <c>SELECT @Total = COUNT(*)</c> style
        /// procedures). Accepts either raw SQL text or a bare stored procedure name.
        /// </summary>
        public Task<SqlResult> ExecuteScalarAsync(
            string commandText,
            IEnumerable<SqlParameter>? parameters = null,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(
                nameof(ExecuteScalarAsync),
                commandText,
                parameters,
                cmd => cmd.ExecuteScalarAsync(cancellationToken),
                cancellationToken);

        /// <summary>
        /// Executes a query and returns every result set as a <see cref="DataSet"/>. Use
        /// <see cref="SqlResult.AsDataTable"/> for the first (and usually only) table, or
        /// <see cref="SqlResult.AsDataSet"/> when the command returns multiple result sets.
        /// </summary>
        public Task<SqlResult> ExecuteQueryAsync(
            string commandText,
            IEnumerable<SqlParameter>? parameters = null,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(
                nameof(ExecuteQueryAsync),
                commandText,
                parameters,
                async cmd =>
                {
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                    var dataSet = new DataSet();
                    var tableIndex = 0;

                    do
                    {
                        var table = new DataTable($"Table{tableIndex++}");
                        table.Load(reader);
                        foreach (DataColumn column in table.Columns) column.ReadOnly = false;
                        dataSet.Tables.Add(table);
                    }
                    while (!reader.IsClosed && await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

                    return (object?)dataSet;
                },
                cancellationToken);

        /// <summary>
        /// Executes an INSERT/UPDATE/DELETE statement, or a stored procedure that doesn't select
        /// rows. <see cref="SqlResult.AsInt32"/> on the result is the number of rows affected.
        /// </summary>
        public Task<SqlResult> ExecuteNonQueryAsync(
            string commandText,
            IEnumerable<SqlParameter>? parameters = null,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(
                nameof(ExecuteNonQueryAsync),
                commandText,
                parameters,
                async cmd => (object?)await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken);

        // ============================================================
        // CORE PIPELINE
        // ============================================================

        private async Task<SqlResult> ExecuteAsync(
            string operationName,
            string commandText,
            IEnumerable<SqlParameter>? parameters,
            Func<SqlCommand, Task<object?>> execute,
            CancellationToken cancellationToken)
        {
            var sqlResult = new SqlResult();
            var policy = CreateRetryPolicy(operationName, commandText);

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(commandText, connection)
            {
                CommandType = SqlCommandTypeDetector.Detect(commandText)
            };

            if (_options.CommandTimeoutSeconds.HasValue)
                command.CommandTimeout = _options.CommandTimeoutSeconds.Value;

            AttachParameters(parameters, command);

            try
            {
                sqlResult.Result = await policy.ExecuteAsync(async ct =>
                {
                    if (connection.State != ConnectionState.Open)
                        await connection.OpenAsync(ct).ConfigureAwait(false);

                    return await execute(command).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);

                CaptureOutputParameters(command, sqlResult);
                sqlResult.IsCriticalError = false;
            }
            catch (SqlException ex)
            {
                sqlResult.ErrorMessage = SqlExceptionFormatter.Format(commandText, ex);
                sqlResult.IsCriticalError = true;
                _logger?.LogError(ex, "{Operation} failed for {CommandText} after retries.", operationName, commandText);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sqlResult.ErrorMessage = ex.ToString();
                sqlResult.IsCriticalError = true;
                _logger?.LogError(ex, "{Operation} failed unexpectedly for {CommandText}.", operationName, commandText);
            }
            finally
            {
                command.Parameters.Clear();
                if (connection.State == ConnectionState.Open) connection.Close();
            }

            return sqlResult;
        }

        private IAsyncPolicy CreateRetryPolicy(string operationName, string commandText)
        {
            var isTransient = _options.IsTransient ?? (ex => !_options.NonRetryableErrorNumbers.Contains(ex.Number));

            return Policy
                .Handle<SqlException>(isTransient)
                .WaitAndRetryAsync(
                    retryCount: _options.RetryCount,
                    sleepDurationProvider: attempt => _options.GetDelayForAttempt(attempt),
                    onRetry: (exception, delay, attempt, _) =>
                    {
                        var sqlException = (SqlException)exception;

                        _logger?.LogWarning(
                            exception,
                            "{Operation} attempt {Attempt}/{MaxAttempts} for {CommandText} failed; retrying in {Delay}.",
                            operationName, attempt, _options.RetryCount, commandText, delay);

                        Retrying?.Invoke(this, new SqlRetryEventArgs(operationName, commandText, attempt, _options.RetryCount, delay, sqlException));
                    });
        }

        /// <summary>
        /// Attaches caller-supplied parameters and, for stored procedures, ensures output
        /// parameters have a size and a RETURN value parameter is present.
        /// </summary>
        private void AttachParameters(IEnumerable<SqlParameter>? parameters, SqlCommand command)
        {
            if (command.CommandType != CommandType.StoredProcedure)
            {
                if (parameters != null) command.Parameters.AddRange(parameters.ToArray());
                return;
            }

            if (parameters != null)
            {
                foreach (var p in parameters)
                {
                    if ((p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.InputOutput) && p.Size == 0)
                        p.Size = _options.OutputParameterDefaultSize;

                    command.Parameters.Add(p);
                }
            }

            var hasReturnValueParameter = command.Parameters
                .OfType<SqlParameter>()
                .Any(p => p.Direction == ParameterDirection.ReturnValue);

            if (!hasReturnValueParameter)
            {
                command.Parameters.Add(new SqlParameter("@ReturnValue", SqlDbType.Int)
                {
                    Direction = ParameterDirection.ReturnValue
                });
            }
        }

        private static void CaptureOutputParameters(SqlCommand command, SqlResult sqlResult)
        {
            if (command.CommandType != CommandType.StoredProcedure) return;

            foreach (SqlParameter p in command.Parameters)
            {
                if (p.Direction == ParameterDirection.ReturnValue)
                {
                    sqlResult.ReturnValue = p.Value is int i ? i : Convert.ToInt32(p.Value ?? 0);
                }
                else if (p.Direction == ParameterDirection.Output || p.Direction == ParameterDirection.InputOutput)
                {
                    sqlResult.OutputParameters[p.ParameterName] = p.Value == DBNull.Value ? null : p.Value;
                }
            }
        }
    }
}
