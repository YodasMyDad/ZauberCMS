using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping
{
    public class UserDbMapping : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(e => e.ExtendedData).ToJsonConversion(3500);
            builder.Property(x => x.PasswordHash).HasMaxLength(300);
            builder.Property(x => x.SecurityStamp).HasMaxLength(3000);
            builder.Property(x => x.ConcurrencyStamp).HasMaxLength(3000);
            builder.Property(x => x.PhoneNumber).HasMaxLength(100);
            builder.Property(x => x.UserName).HasMaxLength(150);
            builder.Property(x => x.PropertyData).ToJsonConversion();
            //builder.HasOne(x => x.ProfileImage);
        }
    }
}