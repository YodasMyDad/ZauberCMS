using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ZauberCMS.Core.Audit.Mapping;

public class AuditDbMapper : IEntityTypeConfiguration<Models.Audit>
{
    public void Configure(EntityTypeBuilder<Models.Audit> builder)
    {
        builder.ToTable("ZauberAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(3000);
    }
}