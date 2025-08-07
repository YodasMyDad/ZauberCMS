using ZauberCMS.Core.Email.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Email.Interfaces;

public interface IEmailService
{
    Task SendEmailConfirmationAsync(SendEmailConfirmationParameters parameters, CancellationToken cancellationToken = default);
}
