using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Handlers;

public class DeleteLanguageDictionaryHandler(IServiceProvider serviceProvider) : IRequestHandler<DeleteLanguageDictionaryCommand, HandlerResult<LanguageDictionary?>>
{
    public async Task<HandlerResult<LanguageDictionary?>> Handle(DeleteLanguageDictionaryCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<LanguageDictionary>();

        if (request.Id != null)
        {
            var langDict = await dbContext.LanguageDictionaries.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken: cancellationToken);
            if (langDict != null)
            {
                dbContext.LanguageDictionaries.Remove(langDict);
            }
        }
        
        return (await dbContext.SaveChangesAndLog(null, handlerResult, cancellationToken))!;
    }
}