using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

public class DomainDbMapping : IEntityTypeConfiguration<Domain>
{
    public void Configure(EntityTypeBuilder<Domain> builder)
    {
        builder.ToTable("ZauberDomains");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(350);
        
        builder.HasOne(x => x.Language)
            .WithMany(x => x.Domains)
            .HasForeignKey(x => x.LanguageId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}