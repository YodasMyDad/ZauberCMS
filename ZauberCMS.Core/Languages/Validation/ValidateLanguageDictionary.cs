using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Languages.Validation;

public class ValidateLanguageDictionary : IValidate<LanguageDictionary>
{
    public Task<ValidateResult> Validate(LanguageDictionary item)
    {
        var validateResult = new ValidateResult();
        if (item.Key.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add("You cannot leave key name empty");
        }
        
        // Make sure the Language texts are not null
        foreach (var languageText in item.Texts)
        {
            if (languageText.Value.IsNullOrWhiteSpace())
            {
                validateResult.ErrorMessages.Add("You cannot leave the language values empty");
                return Task.FromResult(validateResult);        
            }
        }
        
        return Task.FromResult(validateResult);
    }
}