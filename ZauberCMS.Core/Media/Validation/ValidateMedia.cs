using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Media.Validation;

public class ValidateMedia : IValidate<Models.Media>
{
    public Task<ValidateResult> Validate(Models.Media item)
    {
        var validateResult = new ValidateResult();
        
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add("You cannot leave the name empty");
        }

        return Task.FromResult(validateResult);
    }
}