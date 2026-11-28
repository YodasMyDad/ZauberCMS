using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Membership.Validation;

public class ValidateRole(IStringLocalizer<ZauberText> text) : IValidate<Role>
{
    public Task<ValidateResult> Validate(Role item)
    {
        var validateResult = new ValidateResult();
        
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add(text["membership_nameRequired"]);
        }
        
        if (item.Properties.Any(x => x.Name.IsNullOrWhiteSpace()))
        {
            validateResult.ErrorMessages.Add(text["membership_propertiesMissingName"]);
        }

        return Task.FromResult(validateResult);
    }
}