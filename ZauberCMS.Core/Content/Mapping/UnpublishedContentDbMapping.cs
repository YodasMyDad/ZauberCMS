using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Mapping;

public class UnpublishedContentDbMapping : IEntityTypeConfiguration<UnpublishedContent>
{
    public void Configure(EntityTypeBuilder<UnpublishedContent> builder)
    {
        builder.ToTable("ZauberUnpublishedContent");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.DateCreated).IsRequired();
        builder.Property(x => x.DateUpdated).IsRequired();
        builder.Property(e => e.JsonContent).ToJsonConversion(null);
        
        
    }
}