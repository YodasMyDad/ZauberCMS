using FluentValidation;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Validation;

namespace ZauberCMS.Core.Membership.Validation;

public class UserValidation : BaseFluentValidator<User>
{
    public UserValidation(IStringLocalizer<ZauberText> text)
    {
        RuleFor(p => p.UserName).NotEmpty().WithMessage(_ => text["membership_usernameRequired"]);
        RuleFor(p => p.UserName).MaximumLength(100).WithMessage(_ => text["membership_usernameTooLong"]);

        RuleFor(p => p.Email).NotEmpty().EmailAddress().MaximumLength(100);

        RuleFor(p => p.PhoneNumber).MaximumLength(100).WithMessage(_ => text["membership_phoneNumberTooLong"]);
        RuleFor(p => p.PhoneNumber).Matches("[0-9]+").When(p => !p.PhoneNumber.IsNullOrEmpty());
    }
}