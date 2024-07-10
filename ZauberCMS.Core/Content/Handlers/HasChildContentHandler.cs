using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class HasChildContentHandler(IServiceProvider serviceProvider)  : IRequestHandler<HasChildContentCommand, bool>
{
    public async Task<bool> Handle(HasChildContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        return await dbContext.Contents.AsNoTracking().AnyAsync(c => c.ParentId == request.ParentId, cancellationToken: cancellationToken);
    }
}