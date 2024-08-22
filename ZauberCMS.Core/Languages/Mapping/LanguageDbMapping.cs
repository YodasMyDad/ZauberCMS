using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Mapping;

public class LanguageDbMapping : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("ZauberLanguages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.LanguageIsoCode).HasMaxLength(14);
        builder.Property(x => x.LanguageCultureName).HasMaxLength(100);

        builder.HasMany(x => x.LanguageTexts)
            .WithOne(x => x.Language)
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}