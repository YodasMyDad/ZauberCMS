using System.Threading;
using System.Threading.Tasks;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Interfaces;

public interface IMembershipService
{
    Task<User?> GetUserAsync(GetUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> SaveUserAsync(SaveUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> CreateUpdateUserAsync(CreateUpdateUserParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<User>> DeleteUserAsync(DeleteUserParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<User>> QueryUsersAsync(QueryUsersParameters parameters, CancellationToken cancellationToken = default);

    Task<Role?> GetRoleAsync(GetRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Role>> SaveRoleAsync(SaveRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<HandlerResult<Role>> DeleteRoleAsync(DeleteRoleParameters parameters, CancellationToken cancellationToken = default);
    Task<PaginatedList<Role>> QueryRolesAsync(QueryRolesParameters parameters, CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginUserAsync(LoginUserParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> RegisterUserAsync(RegisterUserParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ExternalLoginAsync(ExternalLoginParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ConfirmEmailAsync(ConfirmEmailParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ForgotPasswordAsync(ForgotPasswordParameters parameters, CancellationToken cancellationToken = default);
    Task<AuthenticationResult> ResetPasswordAsync(ResetPasswordParameters parameters, CancellationToken cancellationToken = default);
    Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}