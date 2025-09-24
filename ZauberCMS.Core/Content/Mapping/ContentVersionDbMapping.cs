using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentVersionDbMapping : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.ToTable("ZauberContentVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.ContentId).IsRequired();
        builder.Property(x => x.VersionNumber).IsRequired();
        builder.Property(x => x.VersionName).HasMaxLength(500);
        builder.Property(x => x.Status).IsRequired().HasDefaultValue(ContentVersionStatus.Draft);
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.Comments).HasMaxLength(2000);
        builder.Property(x => x.IsAutoSave).HasDefaultValue(false);
        builder.Property(x => x.ContentSize).HasDefaultValue(0);

        // JSON columns for snapshots
        builder.Property(e => e.Snapshot).ToJsonConversion(5000);
        builder.Property(e => e.PropertySnapshots).ToJsonConversion(null); // Unlimited for property data
        builder.Property(e => e.BlockListSnapshots).ToJsonConversion(null); // Unlimited for block list data
        builder.Property(e => e.Tags).ToJsonConversion(2000);

        // Indexes for performance
        builder.HasIndex(x => x.ContentId).HasDatabaseName("IX_ContentVersion_ContentId");
        builder.HasIndex(x => new { x.ContentId, x.VersionNumber }).HasDatabaseName("IX_ContentVersion_ContentId_Version");
        builder.HasIndex(x => new { x.ContentId, x.Status }).HasDatabaseName("IX_ContentVersion_ContentId_Status");
        builder.HasIndex(x => new { x.ContentId, x.IsCurrentPublished }).HasDatabaseName("IX_ContentVersion_CurrentPublished");
        builder.HasIndex(x => new { x.ContentId, x.IsLatestDraft }).HasDatabaseName("IX_ContentVersion_LatestDraft");
        builder.HasIndex(x => x.DateCreated).HasDatabaseName("IX_ContentVersion_DateCreated");

        // Foreign key relationships
        builder.HasOne(d => d.CreatedBy)
            .WithMany()
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.ParentVersion)
            .WithMany()
            .HasForeignKey(d => d.ParentVersionId)
            .OnDelete(DeleteBehavior.NoAction);

        // Ensure only one current published version per content
        builder.HasIndex(x => x.ContentId)
            .HasFilter("[IsCurrentPublished] = 1")
            .HasDatabaseName("IX_ContentVersion_UniqueCurrentPublished")
            .IsUnique();

        // Ensure only one latest draft per content
        builder.HasIndex(x => x.ContentId)
            .HasFilter("[IsLatestDraft] = 1")
            .HasDatabaseName("IX_ContentVersion_UniqueLatestDraft")
            .IsUnique();
    }
}
