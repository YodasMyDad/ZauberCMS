using MediatR;
using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Search.Commands;
using ZauberCMS.Core.Search.Models;

namespace ZauberCMS.Core.Search.Handlers;

public class AdminSearchHandler(ZauberDbContext context) : IRequestHandler<AdminSearchCommand, List<AdminSearchResult>>
{
    public async Task<List<AdminSearchResult>> Handle(AdminSearchCommand request, CancellationToken cancellationToken)
    {
        const string sql =  """
                            SELECT Id, Name, 'Content' AS Type FROM ZauberContent WHERE Name LIKE {0}
                            UNION
                            SELECT Id, Name, 'Media' AS Type FROM ZauberMedia WHERE Name LIKE {0}
                            ORDER BY Name
                            """;

        var results = await context.AdminSearchResults
            .FromSqlRaw(sql, $"%{request.SearchTerm}%")
            .Take(request.Amount)
            .ToListAsync(cancellationToken);

        return results;
    }
}