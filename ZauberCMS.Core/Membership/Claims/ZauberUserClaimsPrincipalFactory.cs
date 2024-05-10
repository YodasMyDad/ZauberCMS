using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Claims
{
    public class ZauberUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User>
    {
        private readonly UserManager<User> _userManager;
        private readonly ZauberDbContext _context;

        public ZauberUserClaimsPrincipalFactory(
            UserManager<User> userManager,
            IOptions<IdentityOptions> optionsAccessor, ZauberDbContext context)
                : base(userManager, optionsAccessor)
        {
            _userManager = userManager;
            _context = context;
        }

        public override async Task<ClaimsPrincipal> CreateAsync(User user)
        {
            var dbUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == user.Id);
            user = dbUser!;
            
            var principal = await base.CreateAsync(user);
            var roles = await _userManager.GetRolesAsync(user);

            var claimsToAdd = new List<Claim> {new(Constants.Claims.Md5Hash, user.Email?.ToMd5() ?? string.Empty)};

            /*if (user.ProfileImage?.Url.IsNullOrWhiteSpace() == false)
            {
                claimsToAdd.Add(new Claim(Constants.Claims.ProfileImage, user.ProfileImage.Url));
            }*/

            if (roles.Count > 0)
            {
                foreach (var r in roles)
                {
                    claimsToAdd.Add(new Claim(ClaimTypes.Role, r));
                }
            }

            if (claimsToAdd.Count > 0)
            {
                ((ClaimsIdentity)principal.Identity!).AddClaims(claimsToAdd.ToArray());
            }

            return principal;
        }
    }
}