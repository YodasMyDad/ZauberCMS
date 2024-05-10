using System.Text;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Email.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Providers;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Email.Handlers;

public class SendEmailConfirmationHandler : IRequestHandler<SendEmailConfirmationCommand>
    {
        private readonly UserManager<User> _userManager;
        private readonly ILogger<SendEmailConfirmationHandler> _logger;
        private readonly ProviderService _providerService;
        private readonly ZauberSettings _settings;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SendEmailConfirmationHandler(UserManager<User> userManager,
                                ILogger<SendEmailConfirmationHandler> logger,
                                IOptions<ZauberSettings> gabSettings,
                                IHttpContextAccessor httpContextAccessor, ProviderService providerService)
        {
            _userManager = userManager;
            _logger = logger;
            _settings = gabSettings.Value;
            _httpContextAccessor = httpContextAccessor;
            _providerService = providerService;
        }

        public async Task Handle(SendEmailConfirmationCommand request, CancellationToken cancellationToken)
        {
            var userId = await _userManager.GetUserIdAsync(request.User);

            string code;
            string email;

            // Is this a change of email or a new signup
            var isChange = "false";
            if (request.NewEmailAddress.IsNullOrWhiteSpace())
            {
                code = await _userManager.GenerateEmailConfirmationTokenAsync(request.User);
                email = request.User.Email;
            }
            else
            {
                isChange = "true";
                code = await _userManager.GenerateChangeEmailTokenAsync(request.User, request.NewEmailAddress);
                email = request.NewEmailAddress;
            }
  
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            var callbackUrl = $"{_httpContextAccessor.ToAbsoluteUrl(Constants.Urls.Account.ConfirmEmail)}?userId={userId}&code={code}&change={isChange}&returnUrl={request.ReturnUrl}";
            
            var paragraphs = new List<string> { $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>." };

            await _providerService.EmailProvider!.SendEmailWithTemplateAsync(email!, "Confirm your email", paragraphs);
        }
    }