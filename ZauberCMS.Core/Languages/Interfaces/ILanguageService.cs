using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Languages.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Interfaces;

public interface ILanguageService
{
    Task<Language?> GetLanguageAsync(GetLanguageParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Language>> SaveLanguageAsync(SaveLanguageParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Language>> QueryLanguageAsync(QueryLanguageParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Language?>> DeleteLanguageAsync(DeleteLanguageParameters parameters, CancellationToken cancellationToken = default);

    Task<HandlerResult<LanguageDictionary>> SaveLanguageDictionaryAsync(SaveLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<LanguageDictionary?>> DeleteLanguageDictionaryAsync(DeleteLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default);
    Task<Dictionary<string, Dictionary<string, string>>> GetCachedAllLanguageDictionariesAsync(GetCachedAllLanguageDictionariesParameters parameters, CancellationToken cancellationToken = default);
    Task<DataGridResult<LanguageDictionary>> GetDataGridLanguageDictionaryAsync(DataGridLanguageDictionaryParameters parameters, CancellationToken cancellationToken = default);
}