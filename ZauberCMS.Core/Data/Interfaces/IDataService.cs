using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Data.Interfaces;

public interface IDataService
{
    Task<GlobalData?> GetGlobalDataAsync(GetGlobalDataParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<GlobalData>> SaveGlobalDataAsync(SaveGlobalDataParameters parameters, CancellationToken cancellationToken = default);
    Task<Dictionary<string, IEnumerable<object>>> MultiQueryAsync(MultiQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<DataGridResult<T>> GetDataGridAsync<T>(DataGridParameters<T> parameters, CancellationToken cancellationToken = default) where T : class, ITreeItem;
}