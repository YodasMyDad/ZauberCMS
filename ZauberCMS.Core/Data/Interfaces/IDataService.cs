using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Data.Interfaces;

public interface IDataService
{
    Task<object?> GetGlobalDataAsync(GetGlobalDataParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<object>> SaveGlobalDataAsync(SaveGlobalDataParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<object>> MultiQueryAsync(MultiQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<T>> GetDataGridAsync<T>(DataGridParameters<T> parameters, CancellationToken cancellationToken = default);
}
