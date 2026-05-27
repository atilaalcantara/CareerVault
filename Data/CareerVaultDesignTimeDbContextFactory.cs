using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace CareerVault.Api.Data;

public sealed class CareerVaultDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CareerVaultDbContext>
{
    public CareerVaultDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres nao configurada.");

        var optionsBuilder = new DbContextOptionsBuilder<CareerVaultDbContext>();
        optionsBuilder.UseNpgsql(connectionString, options => options.UseVector());

        return new CareerVaultDbContext(optionsBuilder.Options);
    }
}
