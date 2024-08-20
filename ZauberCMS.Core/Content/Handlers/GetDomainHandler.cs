using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class GetDomainHandler(IServiceProvider serviceProvider) : IRequestHandler<GetDomainCommand, Domain?>
{
    public async Task<Domain?> Handle(GetDomainCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Domains.AsQueryable();

        if (request.AsNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (request.Url != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Url == request.Url, cancellationToken: cancellationToken);
        }

        if (request.Id != null)
        {
            return await query.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
        }

        // Should never get here
        return await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}