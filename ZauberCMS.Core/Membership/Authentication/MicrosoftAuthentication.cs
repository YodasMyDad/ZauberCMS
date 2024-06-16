using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins.Interfaces;

namespace ZauberCMS.Core.Membership.Authentication;

public class MicrosoftAuthentication : IExternalAuthenticationProvider
{
    public void Add(IServiceCollection servicesCollection, AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
        var microsoftId = configuration.GetValue<string>("BlogFodder:Identity:ExternalProviders:Microsoft:ClientId");
        if (!microsoftId.IsNullOrWhiteSpace())
        {
            // https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-5.0
            authenticationBuilder.AddMicrosoftAccount(microsoftOptions =>
            {
                microsoftOptions.ClientId = microsoftId;
                microsoftOptions.ClientSecret = configuration.GetValue<string>("BlogFodder:Identity:ExternalProviders:Microsoft:ClientSecret") ?? "";
            });
        }
    }
}