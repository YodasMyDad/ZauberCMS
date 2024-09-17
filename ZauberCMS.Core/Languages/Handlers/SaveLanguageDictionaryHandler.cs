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

public class SaveLanguageDictionaryHandler(
    IServiceProvider serviceProvider,
    IMapper mapper,
    ICacheService cacheService,
    IMediator mediator,
    AuthenticationStateProvider authenticationStateProvider) : IRequestHandler<SaveLanguageDictionaryCommand, HandlerResult<LanguageDictionary>>
{
    public async Task<HandlerResult<LanguageDictionary>> Handle(SaveLanguageDictionaryCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var user = await userManager.GetUserAsync(authState.User);
        var handlerResult = new HandlerResult<LanguageDictionary>();

        if (request.LanguageDictionary != null)
        {
            var langTexts = request.LanguageDictionary.Texts;

            request.LanguageDictionary.Texts = [];

            // Save the language dictionary first
            var langDictionary =
                dbContext.LanguageDictionaries.FirstOrDefault(x => x.Id == request.LanguageDictionary.Id);
            if (langDictionary == null)
            {
                langDictionary = request.LanguageDictionary;
                dbContext.LanguageDictionaries.Add(langDictionary);
            }
            else
            {
                mapper.Map(request.LanguageDictionary, langDictionary);
            }

            await user.AddAudit(langDictionary, $"Language Dictionary ({langDictionary.Key})",
                AuditExtensions.AuditAction.Update, mediator,
                cancellationToken);
            handlerResult = await dbContext.SaveChangesAndLog(langDictionary, handlerResult, cacheService, cancellationToken);
            if (handlerResult.Success)
            {
                var langTextResult = new HandlerResult<LanguageText>();
                foreach (var languageText in langTexts)
                {
                    var lt = dbContext.LanguageTexts.FirstOrDefault(x => x.Id == languageText.Id);
                    if (lt == null)
                    {
                        lt = languageText;
                        dbContext.LanguageTexts.Add(lt);
                    }
                    else
                    {
                        mapper.Map(languageText, lt);
                    }

                    var saveResult = await dbContext.SaveChangesAndLog(lt, langTextResult, cacheService, cancellationToken);
                    if (!saveResult.Success)
                    {
                        handlerResult.Success = false;
                        handlerResult.Messages = saveResult.Messages;
                        return handlerResult;
                    }
                }
            }
            else
            {
                return handlerResult;
            }

            // Clear Cache
            cacheService.ClearCachedItemsWithPrefix(nameof(LanguageDictionary));
            return handlerResult;
        }

        handlerResult.Success = false;
        return handlerResult;
    }
}