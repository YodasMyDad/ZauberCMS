using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Content.Validation;

public class ValidateContent(IContentService contentService) : IValidate<Models.Content>
{
    public async Task<ValidateResult> Validate(Models.Content item)
    {
        var validateResult = new ValidateResult();
        if (item.Name.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add("You cannot leave the name empty");
        }
        
        // This might be new content, so we need to get the content type manually! 
        var contentType = await contentService.GetContentTypeAsync(new GetContentTypeParameters { Id = item.ContentTypeId });
        if (contentType == null)
        {
            validateResult.ErrorMessages.Add("Content type not found");
        }
        else
        {
            var valuesInDict = item.PropertyData.ToDictionary(x => x.ContentTypePropertyId, x => x);
            foreach (var p in contentType.ContentProperties.Where(x => x.IsRequired))
            {
            
                valuesInDict.TryGetValue(p.Id, out var contentValue);
                if (contentValue != null && contentValue.Value.IsNullOrWhiteSpace())
                {
                    validateResult.ErrorMessages.Add($"{p.Name} is required");
                }
            }    
        }
        
        return validateResult;
    }
}