using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ZauberCMS.Core.Data;

public class SqliteZauberDbContext(DbContextOptions options, IConfiguration configuration) 
    : ZauberDbContext(options, configuration)
{
    private readonly IConfiguration _configuration = configuration;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var section = _configuration.GetSection("Zauber");
        var connectionString = section.GetValue<string>("ConnectionString");
        options.UseSqlite(connectionString, builder => builder.MigrationsHistoryTable(tableName:"ZauberMigrations"));
        #if DEBUG
                options.EnableSensitiveDataLogging();
        #endif
    }
}