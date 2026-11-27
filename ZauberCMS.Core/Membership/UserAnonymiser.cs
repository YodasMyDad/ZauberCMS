using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership;

/// <summary>
/// Deleting a user erases them (ZA-033, Roadmap D35): the row stays, marked deleted, so the pages and versions they
/// edited still point at someone, but nothing about the person is kept. The user name and email address are free
/// for a new account; there is no restore.
/// </summary>
internal static class UserAnonymiser
{
    /// <summary>The user name a deleted user is given.</summary>
    public static string DeletedUserName(Guid id) => $"deleted-{id:N}";

    /// <summary>
    /// Marks <paramref name="user"/> deleted and stages the erasure on <paramref name="dbContext"/>: name, email,
    /// phone, password, profile values and extended data cleared; logins, tokens, claims and roles removed; a new
    /// security stamp, which ends the user's sessions. The caller saves.
    /// </summary>
    public static async Task StageAsync(IZauberDbContext dbContext, User user, CancellationToken cancellationToken)
    {
        user.Deleted = true;
        user.DateUpdated = DateTime.UtcNow;

        // Anyone can register a user name, this one too: if it is taken, a random part makes it free, so nobody can
        // stop a user from being erased by taking their name first
        var userName = DeletedUserName(user.Id);
        var normalised = userName.ToUpperInvariant();
        if (await dbContext.Users.AnyAsync(x => x.NormalizedUserName == normalised && x.Id != user.Id,
                cancellationToken))
        {
            userName = $"{userName}-{Guid.NewGuid():N}";
        }

        user.UserName = userName;
        user.NormalizedUserName = userName.ToUpperInvariant();
        user.Email = null;
        user.NormalizedEmail = null;
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.PasswordHash = null;
        user.TwoFactorEnabled = false;
        user.LockoutEnd = null;
        user.AccessFailedCount = 0;
        user.ExtendedData = new Dictionary<string, object>();
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        var id = user.Id;
        dbContext.UserPropertyValues.RemoveRange(
            await dbContext.UserPropertyValues.Where(x => x.UserId == id).ToListAsync(cancellationToken));
        dbContext.UserLogins.RemoveRange(
            await dbContext.UserLogins.Where(x => x.UserId == id).ToListAsync(cancellationToken));
        dbContext.UserTokens.RemoveRange(
            await dbContext.UserTokens.Where(x => x.UserId == id).ToListAsync(cancellationToken));
        dbContext.UserClaims.RemoveRange(
            await dbContext.UserClaims.Where(x => x.UserId == id).ToListAsync(cancellationToken));
        dbContext.UserRoles.RemoveRange(
            await dbContext.UserRoles.Where(x => x.UserId == id).ToListAsync(cancellationToken));
    }
}
