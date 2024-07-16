using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentTypeDbMapping : IEntityTypeConfiguration<ContentType>
{
    public void Configure(EntityTypeBuilder<ContentType> builder)
    {
        builder.ToTable("ZauberContentTypes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(1000);
        builder.Property(x => x.Alias).HasMaxLength(1000);
        builder.Property(x => x.Icon).HasMaxLength(100);
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.DateUpdated).IsRequired();
        builder.Property(e => e.AllowedChildContentTypes).ToJsonConversion(2000);
        builder.Property(e => e.AvailableContentViews).ToJsonConversion(4000);
        builder.Property(e => e.Tabs).ToJsonConversion(4000);
        
        builder.Property(e => e.ContentProperties).ToJsonConversion(null);
        
        builder.HasIndex(x => x.Name).HasDatabaseName("IX_ZauberContentTypes_Name");
        builder.HasIndex(x => x.Alias).HasDatabaseName("IX_ZauberContentTypes_Alias");
    }
}