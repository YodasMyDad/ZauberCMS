using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Commands;

namespace ZauberCMS.Core.Data.Handlers;

public class MultiQueryHandler(IServiceProvider serviceProvider)
    : IRequestHandler<MultiQueryCommand, Dictionary<string, IEnumerable<object>>>
{
    public async Task<Dictionary<string, IEnumerable<object>>> Handle(MultiQueryCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();

        var results = new Dictionary<string, IEnumerable<object>>();

        foreach (var query in request.Queries)
        {
            var queryResult = await query.ExecuteQuery(dbContext, cancellationToken);
            results.Add(query.Name, queryResult);
        }

        return results;
    }
}