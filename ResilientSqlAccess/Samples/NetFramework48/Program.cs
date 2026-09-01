// Sample usage of ResilientSqlAccess from a .NET Framework 4.8 console app.
// Run Samples/Sql/DemoStoredProcedures.sql against a scratch database first.
//
// No DI container here - this is the kind of app ResilientSqlAccess is meant to drop
// into with zero ceremony: `new ResilientSqlClient(...)` and go. Retry progress is
// observed via the Retrying event instead of an ILogger.

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using ResilientSqlAccess;

namespace ConsoleSample.NetFramework48
{
    internal static class Program
    {
        private static async Task<int> Main()
        {
            var connectionString = Environment.GetEnvironmentVariable("RESILIENT_SQL_CONNECTION_STRING")
                                    ?? "Server=localhost;Database=ResilientSqlAccessDemo;Integrated Security=true;TrustServerCertificate=true;";

            // Exponential backoff: 250ms, 500ms, 1s, 2s (4 retries after the first attempt).
            var options = new SqlRetryOptions
            {
                RetryCount = 4,
                RetryDelay = TimeSpan.FromMilliseconds(250),
                BackoffType = SqlRetryBackoffType.Exponential
            };

            var client = new ResilientSqlClient(connectionString, options);
            client.Retrying += (_, e) =>
                Console.WriteLine($"[retry] {e.OperationName} attempt {e.Attempt}/{e.MaxAttempts}, waiting {e.Delay} - {e.Exception.Message}");

            // 1) Scalar - bare stored procedure name, no "SELECT"/spaces, so it's auto-detected
            //    as CommandType.StoredProcedure.
            var countResult = await client.ExecuteScalarAsync("usp_GetCustomerCount");
            Console.WriteLine(countResult.Succeeded
                ? $"Customer count: {countResult.AsInt32()}"
                : $"GetCustomerCount failed: {countResult.ErrorMessage}");

            // 2) Query with a result set.
            var queryResult = await client.ExecuteQueryAsync(
                "usp_GetCustomersByName",
                new[] { new SqlParameter("@NamePattern", SqlDbType.NVarChar, 200) { Value = "A%" } });

            if (queryResult.Succeeded)
            {
                foreach (DataRow row in queryResult.AsDataTable().Rows)
                    Console.WriteLine($"  Customer #{row["Id"]}: {row["Name"]} <{row["Email"]}>");
            }
            else
            {
                Console.WriteLine($"GetCustomersByName failed: {queryResult.ErrorMessage}");
            }

            // 3) Stored procedure with an output parameter and a RETURN value.
            var insertResult = await client.ExecuteNonQueryAsync(
                "usp_InsertCustomer",
                new[]
                {
                    new SqlParameter("@Name", SqlDbType.NVarChar, 200) { Value = "Ada Lovelace" },
                    new SqlParameter("@Email", SqlDbType.NVarChar, 320) { Value = "ada@example.com" },
                    new SqlParameter("@NewCustomerId", SqlDbType.Int) { Direction = ParameterDirection.Output }
                });

            if (insertResult.Succeeded && insertResult.ReturnValue == 0)
            {
                var newId = insertResult.GetOutputParameter<int>("@NewCustomerId");
                Console.WriteLine($"Inserted customer #{newId}.");
            }
            else if (insertResult.Succeeded && insertResult.ReturnValue == 1)
            {
                Console.WriteLine("Insert skipped: a customer with that email already exists.");
            }
            else
            {
                Console.WriteLine($"InsertCustomer failed: {insertResult.ErrorMessage}");
            }

            return 0;
        }
    }
}
