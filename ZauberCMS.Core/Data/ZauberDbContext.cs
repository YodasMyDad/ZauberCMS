using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Data;

public class ZauberDbContext(DbContextOptions options, IConfiguration configuration)
    : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        var section = configuration.GetSection("Zauber");
        var connectionString = section.GetValue<string>("ConnectionString");
        options.UseSqlServer(connectionString);
        #if DEBUG
                options.EnableSensitiveDataLogging();
        #endif
    }
    
    public DbSet<ContentType> ContentTypes => Set<ContentType>();
    public DbSet<Content.Models.Content> Contents => Set<Content.Models.Content>();
    public DbSet<Media.Models.Media> Medias => Set<Media.Models.Media>();
    public DbSet<Audit.Models.Audit> Audits => Set<Audit.Models.Audit>();
    public DbSet<GlobalData> GlobalDatas => Set<GlobalData>();
    public DbSet<ContentPropertyValue> ContentPropertyValues => Set<ContentPropertyValue>();
    public DbSet<UnpublishedContent> UnpublishedContent => Set<UnpublishedContent>();
    public DbSet<UserPropertyValue> UserPropertyValues => Set<UserPropertyValue>();
    public DbSet<Domain> Domains => Set<Domain>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<LanguageDictionary> LanguageDictionaries => Set<LanguageDictionary>();
    public DbSet<LanguageText> LanguageTexts => Set<LanguageText>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        //TODO - Do I need to pull in configurations from other assemblies?? Will this work as expected??
        /*foreach (var assembly in AssemblyManager.Assemblies)
        {
            if (assembly != null) modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }*/
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        
        // The apply configurations from assembly isn't working for the identity models
        modelBuilder.Entity<User>().ToTable("ZauberUsers");
        modelBuilder.Entity<Role>().ToTable("ZauberRoles");
        modelBuilder.Entity<UserClaim>().ToTable("ZauberUserClaims");
        modelBuilder.Entity<UserRole>().ToTable("ZauberUserRoles");
        modelBuilder.Entity<UserLogin>().ToTable("ZauberUserLogins");
        modelBuilder.Entity<RoleClaim>().ToTable("ZauberRoleClaims");
        modelBuilder.Entity<UserToken>().ToTable("ZauberUserTokens");
    }
}