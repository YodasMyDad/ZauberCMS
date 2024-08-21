using MediatR;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Commands;

public class CachedDomainsCommand : IRequest<List<Domain>>
{
    
}