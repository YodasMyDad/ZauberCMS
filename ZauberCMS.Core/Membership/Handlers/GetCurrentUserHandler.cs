using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class GetCurrentUserHandler(IServiceProvider serviceProvider, AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<GetCurrentUserCommand, User?>
{
    public async Task<User?> Handle(GetCurrentUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        return await userManager.GetUserAsync(authState.User);
    }
}