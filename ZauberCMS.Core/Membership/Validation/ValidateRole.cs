using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Membership.Validation;

public class ValidateRole : IValidate<Role>
{
    public Task<ValidateResult> Validate(Role item)
    {
        var validateResult = new ValidateResult();
        
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add("You cannot leave the name empty");
        }
        
        if (item.Properties.Any(x => x.Name.IsNullOrWhiteSpace()))
        {
            validateResult.ErrorMessages.Add("Some properties are missing a name (and alias)");
        }

        return Task.FromResult(validateResult);
    }
}