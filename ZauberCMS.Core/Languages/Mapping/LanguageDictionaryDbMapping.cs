using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Mapping;

public class LanguageDictionaryDbMapping : IEntityTypeConfiguration<LanguageDictionary>
{
    public void Configure(EntityTypeBuilder<LanguageDictionary> builder)
    {
        builder.ToTable("ZauberLanguageDictionaries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Key).IsRequired().HasMaxLength(1000);
        
        builder.HasMany(d => d.Texts)
            .WithOne(p => p.LanguageDictionary)
            .HasForeignKey(d => d.LanguageDictionaryId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(x => x.Key).HasDatabaseName("IX_LanguageDictionary_Key");
    }
}