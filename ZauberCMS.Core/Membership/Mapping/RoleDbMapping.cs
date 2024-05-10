using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

public class RoleDbMapping : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.ConcurrencyStamp).HasMaxLength(3000);
        builder.Property(e => e.ExtendedData).ToJsonConversion(3000);
    }
}