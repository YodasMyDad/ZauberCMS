using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping
{
    public class RoleClaimDbMapping : IEntityTypeConfiguration<RoleClaim>
    {
        public void Configure(EntityTypeBuilder<RoleClaim> builder)
        {
            builder.Property(x => x.ClaimType).HasMaxLength(3000);
            builder.Property(x => x.ClaimValue).HasMaxLength(3000);
        }
    }
}