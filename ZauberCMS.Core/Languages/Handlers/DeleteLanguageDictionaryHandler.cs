using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Languages.Handlers;

public class DeleteLanguageDictionaryHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<DeleteLanguageDictionaryCommand, HandlerResult<LanguageDictionary?>>
{
    public async Task<HandlerResult<LanguageDictionary?>> Handle(DeleteLanguageDictionaryCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<LanguageDictionary>();

        if (request.Id != null)
        {
            var langDict = await dbContext.LanguageDictionaries.FirstOrDefaultAsync(l => l.Id == request.Id,
                cancellationToken: cancellationToken);
            if (langDict != null)
            {
                await user.AddAudit(langDict, $"Language Dictionary ({langDict.Key})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.LanguageDictionaries.Remove(langDict);
            }
        }

        return (await dbContext.SaveChangesAndLog(null, handlerResult, cacheService, cancellationToken))!;
    }
}