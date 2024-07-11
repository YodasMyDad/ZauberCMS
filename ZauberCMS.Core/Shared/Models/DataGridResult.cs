namespace ZauberCMS.Core.Shared.Models;

public class DataGridResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public int Count { get; set; }
}