using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Languages.Commands;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Handlers;

public class DeleteLanguageHandler(IServiceProvider serviceProvider) : IRequestHandler<DeleteLanguageCommand, HandlerResult<Language?>>
{
    public async Task<HandlerResult<Language?>> Handle(DeleteLanguageCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<Language>();

        if (request.Id != null)
        {
            var language = await dbContext.Languages.FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken: cancellationToken);
            if (language != null)
            {
                dbContext.Languages.Remove(language);
            }
        }
        else
        {
            var language = await dbContext.Languages.FirstOrDefaultAsync(l => l.LanguageIsoCode == request.LanguageIsoCode, cancellationToken: cancellationToken);
            if (language != null)
            {
                dbContext.Languages.Remove(language);
            }
        }
        
        return (await dbContext.SaveChangesAndLog(null, handlerResult, cancellationToken))!;
    }
}