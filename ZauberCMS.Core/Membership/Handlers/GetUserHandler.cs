using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Handlers;

public class GetUserHandler(IServiceProvider serviceProvider, ICacheService cacheService)
    : IRequestHandler<GetUserCommand, User?>
{
    public async Task<User?> Handle(GetUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var cacheKey = GenerateCacheKey(request, dbContext);

        if (request.Cached)
        {
            return await cacheService.GetSetCachedItemAsync(cacheKey, async () => await FetchUserAsync(request, dbContext, cancellationToken));
        }

        return await FetchUserAsync(request, dbContext, cancellationToken);
    }

    private static string GenerateCacheKey(GetUserCommand request, ZauberDbContext dbContext)
    {
        var query = BuildQuery(request, dbContext);
        var queryString = query.ToQueryString();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(queryString));
        return typeof(User).ToCacheKey(Convert.ToBase64String(hash));
    }

    private static IQueryable<User> BuildQuery(GetUserCommand request, ZauberDbContext dbContext)
    {
        var query = dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.PropertyData)
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => x.Id == request.Id);

        return query;
    }

    private static async Task<User?> FetchUserAsync(GetUserCommand request, ZauberDbContext dbContext, CancellationToken cancellationToken)
    {
        var query = BuildQuery(request, dbContext);
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}
