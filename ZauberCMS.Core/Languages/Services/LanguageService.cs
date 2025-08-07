using MediatR;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Interfaces;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Services;

public class LanguageService(IMediator mediator) : ILanguageService
{
    public async Task<Language?> GetLanguageAsync(GetLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetLanguageCommand
        {
            Id = parameters.Id,
            IsoCode = parameters.IsoCode,
            AsNoTracking = parameters.AsNoTracking
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Language>> SaveLanguageAsync(SaveLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveLanguageCommand
        {
            Language = parameters.Language,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Language>> QueryLanguageAsync(QueryLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryLanguageCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking,
            SearchTerm = parameters.SearchTerm
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Language>> DeleteLanguageAsync(DeleteLanguageParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteLanguageCommand
        {
            Id = parameters.Id,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<LanguageDictionary>> SaveLanguageDictionaryAsync(SaveLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveLanguageDictionaryCommand
        {
            LanguageDictionary = parameters.LanguageDictionary
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<LanguageDictionary>> DeleteLanguageDictionaryAsync(DeleteLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteLanguageDictionaryCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetCachedAllLanguageDictionariesAsync(GetCachedAllLanguageDictionariesParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetCachedAllLanguageDictionariesCommand
        {
            LanguageCode = parameters.LanguageCode
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<LanguageDictionary>> GetDataGridLanguageDictionaryAsync(DataGridLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DataGridLanguageDictionaryCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking,
            SearchTerm = parameters.SearchTerm
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}