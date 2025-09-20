using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Data
{
    public interface IZauberDbContext : IDisposable
    {
        // --- Your CMS-specific DbSets ---
        DbSet<ContentType> ContentTypes { get; }
        DbSet<Content.Models.Content> Contents { get; }
        DbSet<Media.Models.Media> Medias { get; }
        DbSet<Audit.Models.Audit> Audits { get; }
        DbSet<GlobalData> GlobalDatas { get; }
        DbSet<ContentPropertyValue> ContentPropertyValues { get; }
        DbSet<UnpublishedContent> UnpublishedContent { get; }
        DbSet<ContentVersion> ContentVersions { get; }
        DbSet<UserPropertyValue> UserPropertyValues { get; }
        DbSet<Domain> Domains { get; }
        DbSet<Language> Languages { get; }
        DbSet<Tag> Tags { get; }
        DbSet<TagItem> TagItems { get; }
        DbSet<LanguageDictionary> LanguageDictionaries { get; }
        DbSet<LanguageText> LanguageTexts { get; }
        DbSet<SeoRedirect> Redirects { get; }
        DbSet<ContentRole> ContentRoles { get; }
        DbSet<MediaRole> MediaRoles { get; }

        // --- Identity-related DbSets ---
        DbSet<User> Users { get; }
        DbSet<Role> Roles { get; }
        DbSet<UserRole> UserRoles { get; }
        DbSet<UserClaim> UserClaims { get; }
        DbSet<UserLogin> UserLogins { get; }
        DbSet<RoleClaim> RoleClaims { get; }
        DbSet<UserToken> UserTokens { get; }

        // --- Common methods ---
        int SaveChanges();
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        DbSet<T> Set<T>() where T : class;
        EntityEntry Entry(object entity);

        // Add these for EF-style usage
        EntityEntry Add(object entity);
        EntityEntry<T> Add<T>(T entity) where T : class;
        void AddRange(params object[] entities);
        void AddRange(IEnumerable<object> entities);

        EntityEntry Update(object entity);
        EntityEntry<T> Update<T>(T entity) where T : class;
        void UpdateRange(params object[] entities);
        void UpdateRange(IEnumerable<object> entities);

        EntityEntry Remove(object entity);
        EntityEntry<T> Remove<T>(T entity) where T : class;
        void RemoveRange(params object[] entities);
        void RemoveRange(IEnumerable<object> entities);
        
        DatabaseFacade Database { get; }
    }
}