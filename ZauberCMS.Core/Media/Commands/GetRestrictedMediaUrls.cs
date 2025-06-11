using MediatR;

namespace ZauberCMS.Core.Media.Commands;

public class GetRestrictedMediaUrls : IRequest<Dictionary<string, Guid>>
{
    public bool Cached { get; set; } = true;
}