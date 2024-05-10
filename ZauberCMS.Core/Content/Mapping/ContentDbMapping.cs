using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Content.Models;
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
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.DateUpdated).IsRequired();
        builder.Property(e => e.ContentPropertyData).ToJsonConversion(4000);
    }
}