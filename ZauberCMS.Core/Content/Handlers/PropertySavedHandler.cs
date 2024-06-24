using MediatR;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Plugins;

namespace ZauberCMS.Core.Content.Handlers;

public class PropertySavedHandler(ExtensionManager extensionManager) : IRequestHandler<PropertySavedCommand, string>
{
    public Task<string> Handle(PropertySavedCommand request, CancellationToken cancellationToken)
    {
        var allPropertySaved = extensionManager.GetInstances<IPropertySaved>(true)
            .Where(x => x.Value.PropertyAlias == request.PropertyAlias);

        foreach (var ps in allPropertySaved)
        {
            request.PropertyValue = ps.Value.Update(request.PropertyValue);
        }
        return Task.FromResult(request.PropertyValue);
    }
}