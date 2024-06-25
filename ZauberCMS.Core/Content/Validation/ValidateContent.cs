using MediatR;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Content.Validation;

public class ValidateContent(IMediator mediator) : IValidate<Models.Content>
{
    public async Task<ValidateResult> Validate(Models.Content item)
    {
        var validateResult = new ValidateResult();
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add("You cannot leave the name empty");
        }
        
        // This might be new content, so we need to get the content type manually! 
        var contentType = await mediator.Send(new GetContentTypeCommand { Id = item.ContentTypeId });
        
        var valuesInDict = item.ContentPropertyData.ToDictionary(x => x.ContentTypePropertyId, x => x);
        foreach (var p in contentType.ContentProperties.Where(x => x.IsRequired))
        {
            
            valuesInDict.TryGetValue(p.Id, out var contentValue);
            if (contentValue != null && contentValue.Value.IsNullOrWhiteSpace())
            {
                validateResult.ErrorMessages.Add($"{p.Name} is required");
            }
        }

        return validateResult;
    }
}