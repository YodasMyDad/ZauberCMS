using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Data.Parameters;

public class DataGridParameters<T> where T : class, ITreeItem
{
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
    public string? OrderBy { get; set; }
    public string? Filter { get; set; }
}