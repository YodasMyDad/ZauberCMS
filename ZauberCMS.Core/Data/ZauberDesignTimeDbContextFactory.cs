using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ZauberCMS.Core.Data
{
    public class ZauberDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ZauberDbContext>
    {
        public ZauberDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ZauberCMS"))
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appSettings.{environment}.json", optional: true)
                .Build();

            var connectionString = configuration.GetSection("Zauber").GetValue<string>("ConnectionString");

            var optionsBuilder = new DbContextOptionsBuilder<ZauberDbContext>();
            optionsBuilder.UseSqlServer(connectionString, builder => builder.MigrationsHistoryTable(tableName: "ZauberMigrations"));

            return new ZauberDbContext(optionsBuilder.Options, configuration);
        }
    }

    public class ZauberSqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteZauberDbContext>
    {
        public SqliteZauberDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ZauberCMS"))
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appSettings.{environment}.json", optional: true)
                .Build();

            var connectionString = configuration.GetSection("Zauber").GetValue<string>("ConnectionString");

            var optionsBuilder = new DbContextOptionsBuilder<SqliteZauberDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            return new SqliteZauberDbContext(optionsBuilder.Options, configuration);
        }
    }

    public class ZauberPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlZauberDbContext>
    {
        public PostgreSqlZauberDbContext CreateDbContext(string[] args)
        {
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ZauberCMS"))
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appSettings.{environment}.json", optional: true)
                .Build();

            var connectionString = configuration.GetSection("Zauber").GetValue<string>("ConnectionString");

            var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlZauberDbContext>();
            optionsBuilder.UseNpgsql(connectionString, builder => builder.MigrationsHistoryTable(tableName: "ZauberMigrations"));

            return new PostgreSqlZauberDbContext(optionsBuilder.Options, configuration);
        }
    }
}
