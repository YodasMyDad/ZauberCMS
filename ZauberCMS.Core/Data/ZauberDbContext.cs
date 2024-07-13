using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Data;

public class ZauberDbContext(DbContextOptions<ZauberDbContext> options)
    : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>(options)
{
    public DbSet<ContentType> ContentTypes => Set<ContentType>();
    public DbSet<Content.Models.Content> Contents => Set<Content.Models.Content>();
    public DbSet<Media.Models.Media> Medias => Set<Media.Models.Media>();
    public DbSet<Audit.Models.Audit> Audits => Set<Audit.Models.Audit>();
    public DbSet<GlobalData> GlobalDatas => Set<GlobalData>();
    public DbSet<ContentPropertyValue> ContentPropertyValues => Set<ContentPropertyValue>();
    public DbSet<UserPropertyValue> UserPropertyValues => Set<UserPropertyValue>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        //TODO - need to pull in configurations from other assemblies
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