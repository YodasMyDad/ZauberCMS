using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

public class PropertyValueDbMapping : IEntityTypeConfiguration<PropertyValue>
{
    public void Configure(EntityTypeBuilder<PropertyValue> builder)
    {
        builder.ToTable("ZauberPropertyValues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Alias).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Value);
        
        builder.HasIndex(x => x.ParentId).HasDatabaseName("IX_ZauberPropertyValues_ParentId");
        builder.HasIndex(x => x.Alias).HasDatabaseName("IX_ZauberPropertyValues_Alias");
    }
}