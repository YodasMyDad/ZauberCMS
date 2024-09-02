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
        
        builder.HasOne(x => x.User)
            .WithMany(x => x.Audits)
            .HasForeignKey(x => x.UserId);
        
        builder.HasOne(x => x.Content)
            .WithMany(x =>x.Audits)
            .HasForeignKey(x => x.ContentId);
        
        builder.HasOne(x => x.Media)
            .WithMany(x =>x.Audits)
            .HasForeignKey(x => x.MediaId);
    }
}