// Sample usage of ResilientSqlAccess from a .NET 8 console app, wired up through the
// generic host's DI container (the idiomatic way to obtain an ILogger in modern .NET).
// Run Samples/Sql/DemoStoredProcedures.sql against a scratch database first.

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ResilientSqlAccess;

var connectionString = Environment.GetEnvironmentVariable("RESILIENT_SQL_CONNECTION_STRING")
                        ?? "Server=localhost;Database=ResilientSqlAccessDemo;Integrated Security=true;TrustServerCertificate=true;";

var builder = Host.CreateApplicationBuilder(args);

// Linear backoff: 500ms, 1s, 1.5s (3 retries after the first attempt) - retry progress
// shows up through the injected ILogger<ResilientSqlClient> here instead of the
// Retrying event used in the .NET Framework sample.
builder.Services.AddSingleton(sp => new ResilientSqlClient(
    connectionString,
    new SqlRetryOptions
    {
        RetryCount = 3,
        RetryDelay = TimeSpan.FromMilliseconds(500),
        BackoffType = SqlRetryBackoffType.Linear
    },
    sp.GetRequiredService<ILogger<ResilientSqlClient>>()));

using var host = builder.Build();
var client = host.Services.GetRequiredService<ResilientSqlClient>();

// 1) Scalar - bare stored procedure name, auto-detected as CommandType.StoredProcedure.
var countResult = await client.ExecuteScalarAsync("usp_GetCustomerCount");
Console.WriteLine(countResult.Succeeded
    ? $"Customer count: {countResult.AsInt32()}"
    : $"GetCustomerCount failed: {countResult.ErrorMessage}");

// 2) Query with a result set.
var queryResult = await client.ExecuteQueryAsync(
    "usp_GetCustomersByName",
    [new SqlParameter("@NamePattern", SqlDbType.NVarChar, 200) { Value = "A%" }]);

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
    [
        new SqlParameter("@Name", SqlDbType.NVarChar, 200) { Value = "Grace Hopper" },
        new SqlParameter("@Email", SqlDbType.NVarChar, 320) { Value = "grace@example.com" },
        new SqlParameter("@NewCustomerId", SqlDbType.Int) { Direction = ParameterDirection.Output }
    ]);

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
