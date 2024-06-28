using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentPropertyValueDbMapping : IEntityTypeConfiguration<ContentPropertyValue>
{
    public void Configure(EntityTypeBuilder<ContentPropertyValue> builder)
    {
        builder.ToTable("ZauberContentPropertyValues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Alias).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Value);
        
        builder.HasIndex(x => x.Alias).HasDatabaseName("IX_ZauberContentPropertyValue_Alias");
    }
}