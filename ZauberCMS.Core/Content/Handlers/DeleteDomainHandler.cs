﻿using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class DeleteDomainHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider,
    ExtensionManager extensionManager) : IRequestHandler<DeleteDomainCommand, HandlerResult<Domain?>>
{
    public async Task<HandlerResult<Domain?>> Handle(DeleteDomainCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Domain>();
        Domain? domain = null;
        if (request.Id != null)
        {
            domain =
                await dbContext.Domains.FirstOrDefaultAsync(l => l.Id == request.Id,
                    cancellationToken: cancellationToken);
            if (domain != null)
            {
                await user.AddAudit(domain, $"Domain ({domain.Url})", AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Domains.Remove(domain);
            }
        }
        else
        {
            domain = await dbContext.Domains.FirstOrDefaultAsync(l => l.ContentId == request.ContentId,
                cancellationToken: cancellationToken);
            if (domain != null)
            {
                await user.AddAudit(domain, $"Domain ({domain.Url})", AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Domains.Remove(domain);
            }
        }

        return (await dbContext.SaveChangesAndLog(domain, handlerResult, cacheService, extensionManager, cancellationToken))!;
    }
}