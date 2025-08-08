using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ZauberCMS.Core.Data;

public class SqliteZauberDbContext(
    DbContextOptions<SqliteZauberDbContext> options, 
    IConfiguration configuration) 
    : ZauberDbContextBase(options, configuration), IZauberDbContext
{
    private readonly IConfiguration _configuration = configuration;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var section = _configuration.GetSection("Zauber");
        var connectionString = section.GetValue<string>("ConnectionString");
        options.UseSqlite(connectionString, builder =>
        {
            builder.MigrationsHistoryTable(tableName: "ZauberMigrations");
            builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        });
        #if DEBUG
                options.EnableSensitiveDataLogging();
        #endif
        options
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

    }
}