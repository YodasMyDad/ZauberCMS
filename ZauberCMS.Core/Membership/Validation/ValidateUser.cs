using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Membership.Validation;

public class ValidateUser(IStringLocalizer<ZauberText> text) : IValidate<User>
{
    public Task<ValidateResult> Validate(User item)
    {
        var validateResult = new ValidateResult();
        if (item.UserName.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add(text["membership_nameRequired"]);
        }
        
        var roles = item!.UserRoles.Select(x => x.Role);
        var enumerable = roles as Role[] ?? roles.ToArray();
        var contentProperties = enumerable.SelectMany(x => x.Properties).ToList();
        var valuesInDict = item.PropertyData.ToDictionary(x => x.ContentTypePropertyId, x => x);
        foreach (var p in contentProperties.Where(x => x.IsRequired))
        {
            
            valuesInDict.TryGetValue(p.Id, out var contentValue);
            if (contentValue != null && contentValue.Value.IsNullOrWhiteSpace())
            {
                validateResult.ErrorMessages.Add(text["membership_propertyRequired", p.Name ?? string.Empty]);
            }
        }

        return Task.FromResult(validateResult);
    }
}