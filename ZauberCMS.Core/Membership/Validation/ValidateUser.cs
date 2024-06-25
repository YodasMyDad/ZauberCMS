using MediatR;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Validation.Interfaces;
using ZauberCMS.Core.Shared.Validation.Models;

namespace ZauberCMS.Core.Membership.Validation;

public class ValidateUser(IMediator mediator) : IValidate<User>
{
    public Task<ValidateResult> Validate(User item)
    {
        var validateResult = new ValidateResult();
        if (item.UserName.IsNullOrWhiteSpace())
        {
            validateResult.ErrorMessages.Add("You cannot leave the name empty");
        }
        
        var roles = item!.UserRoles.Select(x => x.Role);
        var enumerable = roles as Role[] ?? roles.ToArray();
        var contentProperties = enumerable.SelectMany(x => x.Properties).ToList();
        var valuesInDict = item.PropertyData.ToDictionary(x => x.ContentTypePropertyId, x => x);
        foreach (var p in contentProperties.Where(x => x.IsRequired))
        {
            
            valuesInDict.TryGetValue(p.Id, out var contentValue);
            if (contentValue != null && contentValue.Value.IsNullOrWhiteSpace())
            {
                validateResult.ErrorMessages.Add($"{p.Name} is required");
            }
        }

        return Task.FromResult(validateResult);
    }
}