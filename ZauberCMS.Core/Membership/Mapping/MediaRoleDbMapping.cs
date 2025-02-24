using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

public class MediaRoleDbMapping : IEntityTypeConfiguration<MediaRole>
{
    public void Configure(EntityTypeBuilder<MediaRole> builder)
    {
        builder.ToTable("ZauberMediaRole");
        
        // Define composite primary key
        builder.HasKey(ur => new { ur.MediaId, ur.RoleId });
        
        builder
            .HasOne<Media.Models.Media>(x => x.Media)
            .WithMany(x => x.MediaRoles)
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Role>(x => x.Role)
            .WithMany(x => x.MediaRoles)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}