using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Data;

public class SqliteZauberDbContext(
    DbContextOptions<SqliteZauberDbContext> options, 
    IOptions<ZauberSettings> settings) 
    : ZauberDbContextBase(options), IZauberDbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var connectionString = settings.Value.ConnectionString;
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