using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Media.Validation;

public class ValidateMedia(IStringLocalizer<ZauberText> text) : IValidate<Models.Media>
{
    public Task<ValidateResult> Validate(Models.Media item)
    {
        var validateResult = new ValidateResult();
        
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add(text["media_nameRequired"]);
        }

        return Task.FromResult(validateResult);
    }
}