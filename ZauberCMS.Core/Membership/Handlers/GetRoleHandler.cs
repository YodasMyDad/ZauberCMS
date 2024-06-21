using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class GetRoleHandler(IServiceProvider serviceProvider)
    : IRequestHandler<GetRoleCommand, Role?>
{
    public async Task<Role?> Handle(GetRoleCommand request, CancellationToken cancellationToken)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            var query = dbContext.Roles.AsQueryable();

            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (request.Id != null)
            {
                return await query.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
            }

            // Should never get here
            return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
        }
    }
}