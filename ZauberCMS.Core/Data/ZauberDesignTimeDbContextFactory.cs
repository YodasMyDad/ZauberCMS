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
        
        // Get the connection string and provider from configuration
        var section = configuration.GetSection("Zauber");
        var connectionString = section.GetValue<string>("ConnectionString");
        var databaseProvider = section.GetValue<string>("DatabaseProvider");

        // Configure the DbContext based on the provider
        var optionsBuilder = new DbContextOptionsBuilder<ZauberDbContext>();
        if (databaseProvider == "Sqlite")
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else if (databaseProvider == "SqlServer")
        {
            optionsBuilder.UseSqlServer(connectionString);
        }

        return new ZauberDbContext(optionsBuilder.Options);
    }
}
