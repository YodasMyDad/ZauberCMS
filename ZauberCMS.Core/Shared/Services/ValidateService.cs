using Radzen;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Validation.Interfaces;

namespace ZauberCMS.Core.Shared.Services;

public class ValidateService<T>(ExtensionManager extensionManager, NotificationService notificationService)
{
    private Dictionary<string, IValidate<T>> Validates { get; set; } = extensionManager.GetInstances<IValidate<T>>(true);

    public async Task<bool> CanSave(T item)
    {
        var canSave = true;
        if (Validates.Count != 0)
        {
            foreach (var validate in Validates)
            {
                var result = await validate.Value.Validate(item);
                if (result.ErrorMessages.Count != 0)
                {
                    canSave = false;
                    notificationService.ShowNotifications("Validation Error", NotificationSeverity.Error, result.ErrorMessages);
                }
            }   
        }
        return canSave;
    }
        
}