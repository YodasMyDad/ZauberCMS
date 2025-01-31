using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentFinder
{
    Task<RouteValueDictionary?> TryFindContent(HttpContext context);
    int SortOrder { get; }
}