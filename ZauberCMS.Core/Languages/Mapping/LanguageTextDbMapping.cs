using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Mapping;

public class LanguageTextDbMapping : IEntityTypeConfiguration<LanguageText>
{
    public void Configure(EntityTypeBuilder<LanguageText> builder)
    {
        builder.ToTable("ZauberLanguageTexts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Value).IsRequired().HasMaxLength(1000);
        
        builder.HasIndex(x => x.Value).HasDatabaseName("IX_LanguageText_Value");
    }
}