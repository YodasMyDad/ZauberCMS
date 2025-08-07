using MediatR;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Services;

public class MembershipService(IMediator mediator) : IMembershipService
{
    public async Task<User?> GetUserAsync(GetUserParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetUserCommand
        {
            Id = parameters.Id,
            Email = parameters.Email
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> SaveUserAsync(SaveUserParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveUserCommand
        {
            User = parameters.User,
            UpdatedBy = parameters.UpdatedBy
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> CreateUpdateUserAsync(CreateUpdateUserParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new CreateUpdateUserCommand
        {
            User = parameters.User,
            Password = parameters.Password,
            Roles = parameters.Roles
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteUserCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> QueryUsersAsync(QueryUsersParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryUsersCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking,
            SearchTerm = parameters.SearchTerm,
            RoleId = parameters.RoleId
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<Role?> GetRoleAsync(GetRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetRoleCommand
        {
            Id = parameters.Id,
            Name = parameters.Name
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Role>> SaveRoleAsync(SaveRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SaveRoleCommand
        {
            Role = parameters.Role
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Role>> DeleteRoleAsync(DeleteRoleParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new DeleteRoleCommand
        {
            Id = parameters.Id
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<Role>> QueryRolesAsync(QueryRolesParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new QueryRolesCommand
        {
            AmountPerPage = parameters.AmountPerPage,
            PageIndex = parameters.PageIndex,
            OrderBy = parameters.OrderBy,
            WhereClause = parameters.WhereClause,
            AsNoTracking = parameters.AsNoTracking,
            SearchTerm = parameters.SearchTerm
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> LoginUserAsync(LoginUserParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new LoginUserCommand
        {
            Email = parameters.Email,
            Password = parameters.Password,
            RememberMe = parameters.RememberMe,
            LockoutOnFailure = parameters.LockoutOnFailure,
            TwoFactorCode = parameters.TwoFactorCode,
            TwoFactorRecoveryCode = parameters.TwoFactorRecoveryCode,
            RememberMachine = parameters.RememberMachine
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> RegisterUserAsync(RegisterUserParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new RegisterUserCommand
        {
            Email = parameters.Email,
            Password = parameters.Password,
            FirstName = parameters.FirstName,
            LastName = parameters.LastName,
            PhoneNumber = parameters.PhoneNumber,
            DateOfBirth = parameters.DateOfBirth,
            Bio = parameters.Bio,
            Headline = parameters.Headline,
            SendConfirmationEmail = parameters.SendConfirmationEmail,
            AutoConfirmEmail = parameters.AutoConfirmEmail,
            Roles = parameters.Roles
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> ExternalLoginAsync(ExternalLoginParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new ExternalLoginCommand
        {
            LoginProvider = parameters.LoginProvider,
            ProviderKey = parameters.ProviderKey,
            ProviderDisplayName = parameters.ProviderDisplayName,
            Email = parameters.Email
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> ConfirmEmailAsync(ConfirmEmailParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new ConfirmEmailCommand
        {
            UserId = parameters.UserId,
            Code = parameters.Code,
            ChangedEmail = parameters.ChangedEmail
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> ForgotPasswordAsync(ForgotPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new ForgotPasswordCommand
        {
            Email = parameters.Email,
            CallbackUrl = parameters.CallbackUrl
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<HandlerResult<User>> ResetPasswordAsync(ResetPasswordParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new ResetPasswordCommand
        {
            Email = parameters.Email,
            ResetCode = parameters.ResetCode,
            NewPassword = parameters.NewPassword,
            ConfirmPassword = parameters.ConfirmPassword,
            ReturnUrl = parameters.ReturnUrl
        };
        
        return await mediator.Send(command, cancellationToken);
    }

    public async Task<User?> GetCurrentUserAsync(GetCurrentUserParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new GetCurrentUserCommand
        {
            IncludeRoles = parameters.IncludeRoles
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}