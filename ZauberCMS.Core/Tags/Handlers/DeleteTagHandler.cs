using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;
using ZauberCMS.Core.Tags.Commands;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Handlers;

public class DeleteTagHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<DeleteTagCommand, HandlerResult<Tag?>>
{
    public async Task<HandlerResult<Tag?>> Handle(DeleteTagCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Tag>();

        if (request.Id != null)
        {
            var tag =
                await dbContext.Tags.FirstOrDefaultAsync(l => l.Id == request.Id,
                    cancellationToken: cancellationToken);
            if (tag != null)
            {
                await user.AddAudit(tag, $"Tag ({tag.TagName})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Tags.Remove(tag);
            }
        }
        else
        {
            var tag =
                await dbContext.Tags.FirstOrDefaultAsync(l => l.TagName == request.TagName,
                    cancellationToken: cancellationToken);
            if (tag != null)
            {
                await user.AddAudit(tag, $"Tag ({tag.TagName})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Tags.Remove(tag);
            }
        }

        return (await dbContext.SaveChangesAndLog(null, handlerResult, cacheService, cancellationToken))!;
    }
}