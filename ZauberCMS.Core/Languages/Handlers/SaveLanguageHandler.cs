using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Languages.Handlers;

public class SaveLanguageHandler(
    IServiceProvider serviceProvider,
    IMediator mediator,
    ICacheService cacheService,
    AuthenticationStateProvider authenticationStateProvider)
    : IRequestHandler<SaveLanguageCommand, HandlerResult<Language>>
{
    public async Task<HandlerResult<Language>> Handle(SaveLanguageCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<Language>();

        if (request.CultureInfo != null)
        {
            var isUpdate = false;

            var language = new Language();
            if (request.Id != null)
            {
                var lang = dbContext.Languages.FirstOrDefault(x => x.Id == request.Id);
                if (lang != null)
                {
                    if (request.CultureInfo.Name == lang.LanguageIsoCode)
                    {
                        // Just return if they are trying to save the same culture
                        handlerResult.Success = true;
                        return handlerResult;
                    }

                    isUpdate = true;
                    language = lang;
                }
            }

            // Does this already exist
            var existing = dbContext.Languages.FirstOrDefault(x => x.LanguageIsoCode == request.CultureInfo.Name);
            if (existing != null)
            {
                handlerResult.AddMessage("Language already exists", ResultMessageType.Error);
                return handlerResult;
            }

            language.LanguageCultureName = request.CultureInfo.EnglishName;
            language.LanguageIsoCode = request.CultureInfo.Name;

            if (!isUpdate)
            {
                dbContext.Languages.Add(language);
            }
            else
            {
                language.DateUpdated = DateTime.UtcNow;
            }

            await user.AddAudit(language, $"Language ({language.LanguageCultureName})",
                isUpdate ? AuditExtensions.AuditAction.Update : AuditExtensions.AuditAction.Create, mediator,
                cancellationToken);
            return await dbContext.SaveChangesAndLog(language, handlerResult, cacheService, cancellationToken);
        }

        handlerResult.AddMessage("CultureInfo is null", ResultMessageType.Error);
        return handlerResult;
    }
}