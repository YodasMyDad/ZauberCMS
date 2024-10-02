using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Mapping;

public class TagDbMapping : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("ZauberTags");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.TagName).HasMaxLength(1000);
        builder.Property(x => x.Slug).HasMaxLength(1000);

        builder.Ignore(x => x.Count);

        builder.HasIndex(x => x.TagName).HasDatabaseName("IX_ZauberTag_TagName");
        builder.HasIndex(x => x.Slug).HasDatabaseName("IX_ZauberTag_Slug");
    }
}