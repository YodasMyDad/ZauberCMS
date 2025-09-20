using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Data
{
    public abstract class ZauberDbContextBase(DbContextOptions options, IConfiguration configuration)
        : IdentityDbContext<User, Role, Guid, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>(options)
    {
        // ReSharper disable once UnusedMember.Local
        private readonly IConfiguration _configuration = configuration;
        
        // All DbSets
        public DbSet<ContentType> ContentTypes => Set<ContentType>();
        public DbSet<Content.Models.Content> Contents => Set<Content.Models.Content>();
        public DbSet<Media.Models.Media> Medias => Set<Media.Models.Media>();
        public DbSet<Audit.Models.Audit> Audits => Set<Audit.Models.Audit>();
        public DbSet<GlobalData> GlobalDatas => Set<GlobalData>();
        public DbSet<ContentPropertyValue> ContentPropertyValues => Set<ContentPropertyValue>();
        public DbSet<UnpublishedContent> UnpublishedContent => Set<UnpublishedContent>();
        public DbSet<ContentVersion> ContentVersions => Set<ContentVersion>();
        public DbSet<UserPropertyValue> UserPropertyValues => Set<UserPropertyValue>();
        public DbSet<Domain> Domains => Set<Domain>();
        public DbSet<Language> Languages => Set<Language>();
        public DbSet<Tag> Tags => Set<Tag>();
        public DbSet<TagItem> TagItems => Set<TagItem>();
        public DbSet<LanguageDictionary> LanguageDictionaries => Set<LanguageDictionary>();
        public DbSet<LanguageText> LanguageTexts => Set<LanguageText>();
        public DbSet<SeoRedirect> Redirects => Set<SeoRedirect>();
        public DbSet<ContentRole> ContentRoles => Set<ContentRole>();
        public DbSet<MediaRole> MediaRoles => Set<MediaRole>();

        // Common OnModelCreating code
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            modelBuilder.Entity<User>().ToTable("ZauberUsers");
            modelBuilder.Entity<Role>().ToTable("ZauberRoles");
            modelBuilder.Entity<UserClaim>().ToTable("ZauberUserClaims");
            modelBuilder.Entity<UserRole>().ToTable("ZauberUserRoles");
            modelBuilder.Entity<UserLogin>().ToTable("ZauberUserLogins");
            modelBuilder.Entity<RoleClaim>().ToTable("ZauberRoleClaims");
            modelBuilder.Entity<UserToken>().ToTable("ZauberUserTokens");
        }
        
        public new DatabaseFacade Database => base.Database;
    }
}
