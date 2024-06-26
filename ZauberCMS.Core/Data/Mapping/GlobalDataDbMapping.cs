using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Data.Mapping;

public class GlobalDataDbMapping : IEntityTypeConfiguration<GlobalData>
{
    public void Configure(EntityTypeBuilder<GlobalData> builder)
    {
        builder.ToTable("ZauberGlobalData");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Alias).IsRequired().HasMaxLength(1000);
        builder.HasIndex(x => x.Alias).HasDatabaseName("IX_GlobalDataAlias");
    }
}