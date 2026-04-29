using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Data
{
    public class ZauberDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ZauberDbContext>
    {
        private readonly IOptions<ZauberSettings> _zauberSettings;

        public ZauberDesignTimeDbContextFactory(IOptions<ZauberSettings> zauberSettings)
        {
            _zauberSettings = zauberSettings;
        }
        
        public ZauberDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ZauberDbContext>();
            optionsBuilder.UseSqlServer(_zauberSettings.Value.ConnectionString, builder => builder.MigrationsHistoryTable(tableName: "ZauberMigrations"));

            return new ZauberDbContext(optionsBuilder.Options, _zauberSettings);
        }
    }
    
    public class ZauberSqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SqliteZauberDbContext>
    {
        private readonly IOptions<ZauberSettings> _zauberSettings;

        public ZauberSqliteDesignTimeDbContextFactory(IOptions<ZauberSettings> zauberSettings)
        {
            _zauberSettings = zauberSettings;
        }

        public SqliteZauberDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SqliteZauberDbContext>();
            optionsBuilder.UseSqlite(_zauberSettings.Value.ConnectionString);

            return new SqliteZauberDbContext(optionsBuilder.Options, _zauberSettings);
        }
    }

    public class ZauberPostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlZauberDbContext>
    {
        private readonly IOptions<ZauberSettings> _zauberSettings;

        public ZauberPostgreSqlDesignTimeDbContextFactory(IOptions<ZauberSettings> zauberSettings)
        {
            _zauberSettings = zauberSettings;
        }

        public PostgreSqlZauberDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlZauberDbContext>();
            optionsBuilder.UseNpgsql(_zauberSettings.Value.ConnectionString, builder => builder.MigrationsHistoryTable(tableName: "ZauberMigrations"));

            return new PostgreSqlZauberDbContext(optionsBuilder.Options, _zauberSettings);
        }
    }
}
