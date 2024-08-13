using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentDbMapping : IEntityTypeConfiguration<Models.Content>
{
    public void Configure(EntityTypeBuilder<Models.Content> builder)
    {
        builder.ToTable("ZauberContent");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(1000);
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.ContentTypeAlias).HasMaxLength(1000);
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.DateUpdated).IsRequired();
        builder.Property(x => x.ViewComponent).HasMaxLength(1000);
        builder.Property(e => e.Path).ToJsonConversion(3000);
        
        builder.HasOne(d => d.ContentType)
            .WithMany(p => p.LinkedContent)
            .HasForeignKey(d => d.ContentTypeId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(d => d.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasOne(d => d.LastUpdatedBy)
            .WithMany()
            .HasForeignKey(d => d.LastUpdatedById)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasOne(d => d.Language)
            .WithMany()
            .HasForeignKey(d => d.LanguageId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasOne(d => d.UnpublishedContent)
            .WithMany()
            .HasForeignKey(d => d.UnpublishedContentId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasMany(c => c.PropertyData)
            .WithOne(x => x.Content)
            .HasForeignKey(p => p.ContentId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasIndex(x => x.Url).HasDatabaseName("IX_ZauberContent_Url");
        builder.HasIndex(x => x.Name).HasDatabaseName("IX_ZauberContent_Name");
        
        builder.Ignore(x => x.InternalRedirectIdAsString);
    }
}