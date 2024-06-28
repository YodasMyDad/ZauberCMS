using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

public class UserPropertyValueDbMapping : IEntityTypeConfiguration<UserPropertyValue>
{
    public void Configure(EntityTypeBuilder<UserPropertyValue> builder)
    {
        builder.ToTable("ZauberUserPropertyValues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Alias).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Value);
        
        builder.HasIndex(x => x.ParentId).HasDatabaseName("IX_ZauberUserPropertyValue_ParentId");
        builder.HasIndex(x => x.Alias).HasDatabaseName("IX_ZauberUserPropertyValue_Alias");
    }
}