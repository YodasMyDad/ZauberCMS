using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Handlers;

public class QueryLanguageHandler(IServiceProvider serviceProvider)
    : IRequestHandler<QueryLanguageCommand, PaginatedList<Language>>
{
        public Task<PaginatedList<Language>> Handle(QueryLanguageCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var query = dbContext.Languages.AsQueryable();

        if (request.Query != null)
        {
            query = request.Query;
        }
        else
        {

            if (request.AsNoTracking)
            {
                query = query.AsNoTracking();
            }

            var idCount = request.Ids.Count;
            if (request.Ids.Count != 0)
            {
                query = query.Where(x => request.Ids.Contains(x.Id));
                request.AmountPerPage = idCount;
            }
        
            if (request.LanguageIsoCodes.Count != 0)
            {
                query = query.Where(x => x.LanguageIsoCode != null && request.LanguageIsoCodes.Contains(x.LanguageIsoCode));
            }   
        }
        
        if (request.WhereClause != null)
        {
            query = query.Where(request.WhereClause);
        }

        query = request.OrderBy switch
        {
            GetLanguageOrderBy.DateCreated => query.OrderBy(p => p.DateCreated),
            GetLanguageOrderBy.DateCreatedDescending => query.OrderByDescending(p => p.DateCreated),
            GetLanguageOrderBy.LanguageIsoCode => query.OrderBy(p => p.LanguageIsoCode),
            GetLanguageOrderBy.LanguageCultureName => query.OrderBy(p => p.LanguageCultureName),
            _ => query.OrderByDescending(p => p.DateCreated)
        };
        
        return Task.FromResult(query.ToPaginatedList(request.PageIndex, request.AmountPerPage));
    }
}