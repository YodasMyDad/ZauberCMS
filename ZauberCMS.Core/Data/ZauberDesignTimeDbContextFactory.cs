using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ZauberCMS.Core.Data;

public class ZauberDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ZauberDbContext>
{
    public ZauberDbContext CreateDbContext(string[] args)
    {
        // Get the environment variable
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ZauberCMS.Web"))
            .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appSettings.{environment}.json", optional: true)
            .Build();

        // Configure the DbContext based on the provider
        var optionsBuilder = new DbContextOptionsBuilder<ZauberDbContext>();


        return new ZauberDbContext(optionsBuilder.Options, configuration);
    }
}

public class ZauberSqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteZauberDbContext>
{
    public SqliteZauberDbContext CreateDbContext(string[] args)
    {
        // Get the environment variable
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ZauberCMS.Web"))
            .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appSettings.{environment}.json", optional: true)
            .Build();

        // Configure the DbContext based on the provider
        var optionsBuilder = new DbContextOptionsBuilder<ZauberDbContext>();


        return new SqliteZauberDbContext(optionsBuilder.Options, configuration);
    }
}
