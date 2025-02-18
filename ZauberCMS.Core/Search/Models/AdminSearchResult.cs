using Microsoft.EntityFrameworkCore;

namespace ZauberCMS.Core.Search.Models;

[Keyless]
public class AdminSearchResult
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
}