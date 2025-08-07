using MediatR;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Email.Interfaces;
using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Email.Services;

public class EmailService(IMediator mediator) : IEmailService
{
    public async Task<HandlerResult<object>> SendEmailConfirmationAsync(SendEmailConfirmationParameters parameters, CancellationToken cancellationToken = default)
    {
        var command = new SendEmailConfirmationCommand
        {
            UserId = parameters.UserId,
            Email = parameters.Email,
            CallbackUrl = parameters.CallbackUrl
        };
        
        return await mediator.Send(command, cancellationToken);
    }
}