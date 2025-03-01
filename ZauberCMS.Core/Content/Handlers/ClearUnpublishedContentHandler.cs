﻿using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Core.Content.Handlers;

public class ClearUnpublishedContentHandler(IServiceProvider serviceProvider, ICacheService cacheService, ExtensionManager extensionManager)
    : IRequestHandler<ClearUnpublishedContentCommand, HandlerResult<UnpublishedContent>>
{
    public async Task<HandlerResult<UnpublishedContent>> Handle(ClearUnpublishedContentCommand request,
        CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        var handlerResult = new HandlerResult<UnpublishedContent>();
        var content =
            await dbContext.Contents.FirstOrDefaultAsync(x => x.Id == request.ContentId,
                cancellationToken: cancellationToken);
        if (content?.UnpublishedContentId != null)
        {
            var uContent =
                await dbContext.UnpublishedContent.FirstOrDefaultAsync(x => x.Id == content.UnpublishedContentId,
                    cancellationToken: cancellationToken);
            if (uContent != null) dbContext.UnpublishedContent.Remove(uContent);
            var result = (await dbContext.SaveChangesAndLog(uContent, handlerResult, cacheService, extensionManager, cancellationToken))!;
            return result;
        }

        return handlerResult;
    }
}