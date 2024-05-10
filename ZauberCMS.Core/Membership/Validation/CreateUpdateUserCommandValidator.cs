using FluentValidation;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Shared.Validation;

namespace ZauberCMS.Core.Membership.Validation
{
    public class CreateUpdateUserCommandValidator : BaseFluentValidator<CreateUpdateUserCommand>
    {
        public CreateUpdateUserCommandValidator()
        {
            
            RuleFor(p => p.NewPasswordConfirmation).NotEmpty().When(p => !p.NewPassword.IsNullOrWhiteSpace());
            RuleFor(p => p.NewPasswordConfirmation).Equal(p => p.NewPassword).When(p => !p.NewPassword.IsNullOrWhiteSpace());
            
            RuleFor(model => model.User)
                .SetValidator(model => new UserValidation());
            
        }
    }
}