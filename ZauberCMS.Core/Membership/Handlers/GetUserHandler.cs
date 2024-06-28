using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Membership.Handlers
{
    public class GetUserHandler(IServiceProvider serviceProvider, ICacheService cacheService)
        : IRequestHandler<GetUserCommand, User?>
    {
        private readonly ICacheService _cacheService = cacheService;

        public async Task<User?> Handle(GetUserCommand request, CancellationToken cancellationToken)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            return await dbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .Include(x => x.PropertyData)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
            
            
            /*return await _cacheService.GetSetCachedItemAsync(typeof(User).ToCacheKey(request.Id.ToString()), async () =>
            {
                return await dbContext.Users
                    .Include(x => x.UserRoles)
                    .ThenInclude(x => x.Role)
                    .AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
            });*/
        }
    }
}