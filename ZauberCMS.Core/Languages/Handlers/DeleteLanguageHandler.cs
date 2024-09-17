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

public class DeleteLanguageHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<DeleteLanguageCommand, HandlerResult<Language?>>
{
    public async Task<HandlerResult<Language?>> Handle(DeleteLanguageCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Language>();

        if (request.Id != null)
        {
            var language =
                await dbContext.Languages.FirstOrDefaultAsync(l => l.Id == request.Id,
                    cancellationToken: cancellationToken);
            if (language != null)
            {
                await user.AddAudit(language, $"Language ({language.LanguageCultureName})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Languages.Remove(language);
            }
        }
        else
        {
            var language = await dbContext.Languages.FirstOrDefaultAsync(
                l => l.LanguageIsoCode == request.LanguageIsoCode, cancellationToken: cancellationToken);
            if (language != null)
            {
                await user.AddAudit(language, $"Language ({language.LanguageCultureName})",
                    AuditExtensions.AuditAction.Delete, mediator,
                    cancellationToken);
                dbContext.Languages.Remove(language);
            }
        }

        return (await dbContext.SaveChangesAndLog(null, handlerResult, cacheService, cancellationToken))!;
    }
}