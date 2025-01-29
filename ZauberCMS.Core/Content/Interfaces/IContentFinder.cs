using Microsoft.AspNetCore.Http;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentFinder
{
    Task<bool> TryFindContent(HttpContext context);
}