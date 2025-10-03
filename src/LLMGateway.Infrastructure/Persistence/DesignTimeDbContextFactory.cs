using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LLMGateway.Infrastructure.Persistence;

/// <summary>
/// Factory for creating GatewayDbContext at design time (migrations, etc.)
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GatewayDbContext>
{
    public GatewayDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<GatewayDbContext>();
        var connectionString = configuration.GetConnectionString("PostgreSQL") ??
            "Host=localhost;Database=llmgateway;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(GatewayDbContext).Assembly.GetName().Name);
        });

        return new GatewayDbContext(optionsBuilder.Options);
    }
}