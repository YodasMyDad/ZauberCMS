using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Seo.Commands;
using ZauberCMS.Core.Seo.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Seo.Handlers;

public class DeleteRedirectHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager) 
    : IRequestHandler<DeleteRedirectCommand, HandlerResult<SeoRedirect?>>
{
    public async Task<HandlerResult<SeoRedirect?>> Handle(DeleteRedirectCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<SeoRedirect>();

        SeoRedirect? redirect = null;
        if (request.Id != null)
        {
            redirect =
                await dbContext.Redirects.FirstOrDefaultAsync(l => l.Id == request.Id,
                    cancellationToken: cancellationToken);
            if (redirect != null)
            {
                await user.AddAudit(redirect, $"Redirect ({redirect.FromUrl} -> {redirect.ToUrl})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Redirects.Remove(redirect);
            }
        }

        return (await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }
}