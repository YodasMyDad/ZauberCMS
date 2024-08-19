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
                    isUpdate = true;
                    language = lang;
                }
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