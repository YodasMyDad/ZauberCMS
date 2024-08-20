using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Handlers;

public class SaveLanguageHandler(
    IServiceProvider serviceProvider,
    IMapper mapper) : IRequestHandler<SaveLanguageCommand, HandlerResult<Language>>
{
    public async Task<HandlerResult<Language>> Handle(SaveLanguageCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
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
            
            return await dbContext.SaveChangesAndLog(language, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("CultureInfo is null", ResultMessageType.Error);
        return handlerResult;
    }
}