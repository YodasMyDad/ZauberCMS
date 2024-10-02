using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Mapping;

public class TagItemDbMapping : IEntityTypeConfiguration<TagItem>
{
    public void Configure(EntityTypeBuilder<TagItem> builder)
    {
        builder.ToTable("ZauberTagItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();

        builder.HasOne(d => d.Tag)
            .WithMany(p => p.TagItems)
            .HasForeignKey(d => d.TagId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(d => d.Content)
            .WithMany(p => p.TagItems)
            .HasForeignKey(d => d.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}