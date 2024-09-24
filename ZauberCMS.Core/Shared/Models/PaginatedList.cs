namespace ZauberCMS.Core.Shared.Models;

public class PaginatedList<T>
{
    public int PageIndex { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

    public PaginatedList()
    {
    }

    public PaginatedList(IQueryable<T> items, int pageIndex, int pageSize)
    {
        var count = items.Count();
        TotalPages = (int) Math.Ceiling(count / (double) pageSize);
        var skip = (pageIndex-1) * pageSize;
        Items = skip > 0 ? items.Skip(skip).Take(pageSize).ToList() : items.Take(pageSize).ToList();
        TotalItems = count;
    }

    public bool HasPreviousPage => PageIndex > 1;

    public bool HasNextPage => PageIndex < TotalPages;
}