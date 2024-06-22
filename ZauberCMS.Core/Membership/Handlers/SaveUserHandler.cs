using AutoMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Handlers;

public class SaveUserHandler(
    IServiceProvider serviceProvider,
    IMapper mapper)
    : IRequestHandler<SaveUserCommand, HandlerResult<User>>
{
    
    public async Task<HandlerResult<User>> Handle(SaveUserCommand request, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
        
        var handlerResult = new HandlerResult<User>();

        if (request.User != null)
        {

            // Get the DB version
            var user = dbContext.Users
                .FirstOrDefault(x => x.Id == request.User.Id);

            if (user == null)
            {
                user = request.User;
                dbContext.Users.Add(user);
                
                // TODO - Check for default role and add, or pass in roles to add user into
            }
            else
            {
                // Map the updated properties
                mapper.Map(request.User, user);   
            }
            
            return await dbContext.SaveChangesAndLog(user, handlerResult, cancellationToken);
        }

        handlerResult.AddMessage("User is null", ResultMessageType.Error);
        return handlerResult;
    }
}