using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Mapping;

public class SeoRedirectDbMapping : IEntityTypeConfiguration<SeoRedirect>
{
    public void Configure(EntityTypeBuilder<SeoRedirect> builder)
    {
        builder.ToTable("ZauberRedirects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.FromUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ToUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.DateUpdated).IsRequired();

        builder.Ignore(x => x.Name);
        
        builder.HasOne(d => d.Domain)
            .WithMany(p => p.Redirects)
            .HasForeignKey(d => d.DomainId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(x => x.FromUrl).HasDatabaseName("IX_ZauberRedirects_FromUrl");
    }
}