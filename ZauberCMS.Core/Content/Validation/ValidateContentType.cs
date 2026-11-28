using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Content.Validation;

public class ValidateContentType(IStringLocalizer<ZauberText> text) : IValidate<ContentType>
{
    public Task<ValidateResult> Validate(ContentType item)
    {
        var validateResult = new ValidateResult();
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add(text["content_nameEmpty"]);
        }
        
        if (item.ContentProperties.Any(x => x.Name.IsNullOrWhiteSpace()))
        {
            validateResult.ErrorMessages.Add(text["content_propertiesMissingName"]);
        }
        
        return Task.FromResult(validateResult);
    }
}