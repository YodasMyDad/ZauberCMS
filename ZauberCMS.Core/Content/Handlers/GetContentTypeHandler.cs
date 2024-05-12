using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class GetContentTypeHandler (IServiceProvider serviceProvider) 
    : IRequestHandler<GetContentTypeCommand, ContentType?>
{
    public async Task<ContentType?> Handle(GetContentTypeCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        return await dbContext.ContentTypes.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken: cancellationToken);
    }
}