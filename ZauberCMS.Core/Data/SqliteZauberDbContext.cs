using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Models;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ensure only one current published version per content
        modelBuilder.Entity<ContentVersion>().HasIndex(x => x.ContentId)
            .HasFilter("\"IsCurrentPublished\" = 1")
            .HasDatabaseName("IX_ContentVersion_UniqueCurrentPublished").IsUnique();
        modelBuilder.Entity<ContentVersion>().HasIndex(x => x.ContentId)
            .HasFilter("\"IsLatestDraft\" = 1")
            .HasDatabaseName("IX_ContentVersion_UniqueLatestDraft").IsUnique();
    }
}