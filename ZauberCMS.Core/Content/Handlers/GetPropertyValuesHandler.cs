using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;

namespace ZauberCMS.Core.Content.Handlers;

public class GetPropertyValuesHandler(IServiceProvider serviceProvider)  : IRequestHandler<GetPropertyValuesCommand, List<PropertyValue>>
{
    public async Task<List<PropertyValue>> Handle(GetPropertyValuesCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        return await dbContext.PropertyValues.AsNoTracking().Where(x => x.ParentId == request.ParentId).ToListAsync(cancellationToken: cancellationToken);
    }
}