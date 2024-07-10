using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Media.Commands;

namespace ZauberCMS.Core.Media.Handlers;

public class HasChildMediaHandler(IServiceProvider serviceProvider)  : IRequestHandler<HasChildMediaCommand, bool>
{
    public async Task<bool> Handle(HasChildMediaCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        return await dbContext.Medias.AsNoTracking().AnyAsync(c => c.ParentId == request.ParentId, cancellationToken: cancellationToken);
    }
}