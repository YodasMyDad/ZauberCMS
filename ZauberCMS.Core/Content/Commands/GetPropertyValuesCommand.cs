using MediatR;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Commands;

public class GetPropertyValuesCommand : IRequest<List<PropertyValue>>
{
    public Guid ParentId { get; set; }
}