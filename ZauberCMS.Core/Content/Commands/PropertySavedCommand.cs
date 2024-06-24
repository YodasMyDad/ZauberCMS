using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class PropertySavedCommand : IRequest<string>
{
    public Type DocumentType { get; set; }
    public string PropertyAlias { get; set; }
    public string PropertyValue { get; set; }
}