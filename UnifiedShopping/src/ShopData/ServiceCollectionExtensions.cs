using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopData;

namespace Microsoft.Extensions.DependencyInjection;

public static class ShopDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shop DbContext. SQLite with Load-Seeding for local dev and demos;
    /// Azure SQL (or any SQL Server) for production. Configured via "Database:Provider".
    /// </summary>
    public static IServiceCollection AddShopData(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("Shop")
            ?? (provider == "Sqlite" ? "Data Source=shop.db" : "REPLACE_WITH_CONNECTION_STRING");

        services.AddDbContext<ShopDbContext>(options =>
        {
            switch (provider)
            {
                case "Sqlite":
                    options.UseSqlite(connectionString);
                    break;
                case "SqlServer":
                    options.UseSqlServer(connectionString, sql =>
                    {
                        // High-traffic settings: retry transient failures, no fan-out to read replicas in the sample.
                        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                    });
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Database:Provider '{provider}' (expected Sqlite or SqlServer).");
            }
        });

        return services;
    }
}
