using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Handlers;

public class GetLanguageHandler(IServiceProvider serviceProvider) : IRequestHandler<GetLanguageCommand, Language?>
{
    public async Task<Language?> Handle(GetLanguageCommand request, CancellationToken cancellationToken)
    {
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            var query = dbContext.Languages.AsQueryable();

            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            if (request.LanguageIsoCode != null)
            {
                return await query.FirstOrDefaultAsync(x => x.LanguageIsoCode == request.LanguageIsoCode, cancellationToken: cancellationToken);
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