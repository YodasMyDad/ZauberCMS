using FluentValidation;
using Microsoft.IdentityModel.Tokens;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Validation;

namespace ZauberCMS.Core.Membership.Validation;

public class UserValidation : BaseFluentValidator<User>
{
    public UserValidation()
    {
        RuleFor(p => p.UserName).NotEmpty().WithMessage("You must enter a username");
        RuleFor(p => p.UserName).MaximumLength(100).WithMessage("Cannot be longer than 100 characters");

        RuleFor(p => p.Email).NotEmpty().EmailAddress().MaximumLength(100);

        RuleFor(p => p.PhoneNumber).MaximumLength(100).WithMessage("PhoneNumber Cannot be longer than 100 characters");
        RuleFor(p => p.PhoneNumber).Matches("[0-9]+").When(p => !p.PhoneNumber.IsNullOrEmpty());
    }
}