using MediatR;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Content.Validation;

public class ValidateContentType : IValidate<ContentType>
{
    public Task<ValidateResult> Validate(ContentType item)
    {
        var validateResult = new ValidateResult();
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add("You cannot leave the name empty");
        }
        
        if (item.ContentProperties.Any(x => x.Name.IsNullOrWhiteSpace()))
        {
            validateResult.ErrorMessages.Add("Some properties are missing a name (and alias)");
        }
        
        return Task.FromResult(validateResult);
    }
}