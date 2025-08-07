using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Email.Interfaces;

public interface IEmailService
{
    Task<HandlerResult<object>> SendEmailConfirmationAsync(SendEmailConfirmationParameters parameters, CancellationToken cancellationToken = default);
}
