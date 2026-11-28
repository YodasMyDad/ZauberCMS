using FluentValidation;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Validation;

namespace ZauberCMS.Core.Membership.Validation
{
    public class CreateUpdateUserCommandValidator : BaseFluentValidator<CreateUpdateUserParameters>
    {
        public CreateUpdateUserCommandValidator(IStringLocalizer<ZauberText> text)
        {
            
            RuleFor(p => p.NewPasswordConfirmation).NotEmpty().When(p => !p.NewPassword.IsNullOrWhiteSpace());
            RuleFor(p => p.NewPasswordConfirmation).Equal(p => p.NewPassword).When(p => !p.NewPassword.IsNullOrWhiteSpace());
            
            RuleFor(model => model.User)
                .SetValidator(model => new UserValidation(text));
            
        }
    }
}