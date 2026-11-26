namespace ZauberCMS.Core.Shared.Models;

public class PaginatedList<T>
{
    public PaginatedList()
    {
    }
    
    public int PageIndex { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public IEnumerable<T> Items { get; set; } = [];
    public bool IsFirstPage => PageIndex <= 1;
    public bool IsLastPage => PageIndex >= TotalPages;
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;
    [Obsolete("Use ToPaginatedListAsync. This blocks the thread on its queries.")]
    public PaginatedList(IQueryable<T> items, int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        var skip = (pageIndex-1) * pageSize;
        var list = skip > 0 ? items.Skip(skip).Take(pageSize).ToList() : items.Take(pageSize).ToList();
        // A first page that isn't full holds every item: no COUNT query needed (ZA-057)
        var count = skip <= 0 && list.Count < pageSize ? list.Count : items.Count();
        Items = list;
        TotalPages = (int) Math.Ceiling(count / (double) pageSize);
        TotalItems = count;
    }
}