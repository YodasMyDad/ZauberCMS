using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
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

public class SaveRedirectHandler(
    IServiceProvider serviceProvider,
    ICacheService cacheService,
    IMapper mapper,
    IMediator mediator,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager)
    : IRequestHandler<SaveRedirectCommand, HandlerResult<SeoRedirect>>
{
    public async Task<HandlerResult<SeoRedirect>> Handle(SaveRedirectCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        
        var handlerResult = new HandlerResult<SeoRedirect>();
        var isUpdate = false;
        if (request.Redirect != null)
        {
            // Get the DB version
            var redirect = dbContext.Redirects
                .FirstOrDefault(x => x.Id == request.Redirect.Id);

            if (redirect == null)
            {
                redirect = request.Redirect;
                dbContext.Redirects.Add(redirect);
            }
            else
            {
                isUpdate = true;
                // Map the updated properties
                mapper.Map(request.Redirect, redirect);
                redirect.DateUpdated = DateTime.UtcNow;                
            }
            
            await user.AddAudit(redirect, redirect.FromUrl, isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, mediator, cancellationToken);
            return await dbContext.SaveChangesAndLog(redirect, handlerResult, cacheService, extensionManager, cancellationToken);
        }

        handlerResult.AddMessage("Redirect is null", ResultMessageType.Error);
        return handlerResult;
    }
}