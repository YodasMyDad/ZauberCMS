using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Interfaces;

public interface IMembershipService
{
    Task<User?> GetUserAsync(GetUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> SaveUserAsync(SaveUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> CreateUpdateUserAsync(CreateUpdateUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> QueryUsersAsync(QueryUsersParameters parameters, CancellationToken cancellationToken = default);
    Task<Role?> GetRoleAsync(GetRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Role>> SaveRoleAsync(SaveRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Role>> DeleteRoleAsync(DeleteRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Role>> QueryRolesAsync(QueryRolesParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> LoginUserAsync(LoginUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> RegisterUserAsync(RegisterUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> ExternalLoginAsync(ExternalLoginParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> ConfirmEmailAsync(ConfirmEmailParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> ForgotPasswordAsync(ForgotPasswordParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> ResetPasswordAsync(ResetPasswordParameters parameters, CancellationToken cancellationToken = default);
    Task<User?> GetCurrentUserAsync(GetCurrentUserParameters parameters, CancellationToken cancellationToken = default);
}
