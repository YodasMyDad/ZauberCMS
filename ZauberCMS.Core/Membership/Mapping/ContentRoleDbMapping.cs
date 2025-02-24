using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

public class ContentRoleDbMapping : IEntityTypeConfiguration<ContentRole>
{
    public void Configure(EntityTypeBuilder<ContentRole> builder)
    {
        builder.ToTable("ZauberContentRole");
        
        // Define composite primary key
        builder.HasKey(ur => new { ur.ContentId, ur.RoleId });
        
        builder
            .HasOne<Content.Models.Content>(ur => ur.Content)
            .WithMany(u => u.ContentRoles)
            .HasForeignKey(ur => ur.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Role>(ur => ur.Role)
            .WithMany(r => r.ContentRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}