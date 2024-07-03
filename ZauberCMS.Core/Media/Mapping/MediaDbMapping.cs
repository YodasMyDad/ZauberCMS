using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Media.Mapping;

public class MediaDbMapping : IEntityTypeConfiguration<Models.Media>
{
    public void Configure(EntityTypeBuilder<Models.Media> builder)
    {
        builder.ToTable("ZauberMedia");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(1000);
        builder.Property(x => x.Url).HasMaxLength(1000);
        builder.Property(x => x.AltTag).HasMaxLength(1000);
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.DateUpdated).IsRequired();
        builder.Property(e => e.ExtendedData).ToJsonConversion(4000);
        
        builder.HasOne(d => d.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(d => d.ParentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.Url).HasDatabaseName("IX_ZauberMedia_Url");
        builder.HasIndex(x => x.Name).HasDatabaseName("IX_ZauberMedia_Name");
    }
}