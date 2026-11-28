using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Languages.Validation;

public class ValidateLanguageDictionary(IStringLocalizer<ZauberText> text) : IValidate<LanguageDictionary>
{
    public Task<ValidateResult> Validate(LanguageDictionary item)
    {
        var validateResult = new ValidateResult();
        if (item.Key.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add(text["languages_dictionaryKeyRequired"]);
        }
        
        // Make sure the Language texts are not null
        foreach (var languageText in item.Texts)
        {
            if (languageText.Value.IsNullOrWhiteSpace())
            {
                validateResult.ErrorMessages.Add(text["languages_dictionaryValuesRequired"]);
                return Task.FromResult(validateResult);        
            }
        }
        
        return Task.FromResult(validateResult);
    }
}