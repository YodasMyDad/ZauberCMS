using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data.Commands;
using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Data.Handlers;

public class GetGlobalDataHandler(IServiceProvider serviceProvider) : IRequestHandler<GetGlobalDataCommand, GlobalData?>
{
    public async Task<GlobalData?> Handle(GetGlobalDataCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        return await dbContext.GlobalDatas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Alias == request.Alias, cancellationToken);
    }
}