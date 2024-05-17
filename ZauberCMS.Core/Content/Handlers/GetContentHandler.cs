using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentHandler (IServiceProvider serviceProvider) 
    : IRequestHandler<GetContentCommand, Models.Content?>
{
    public async Task<Models.Content?> Handle(GetContentCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        return await dbContext.Content
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
    }
}