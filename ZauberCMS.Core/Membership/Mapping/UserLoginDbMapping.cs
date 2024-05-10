using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping
{
    public class UserLoginDbMapping : IEntityTypeConfiguration<UserLogin>
    {
        public void Configure(EntityTypeBuilder<UserLogin> builder)
        {
            builder.Property(x => x.ProviderDisplayName).HasMaxLength(3000);
        }
    }
}