using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins.Interfaces;

namespace ZauberCMS.Core.Membership.Authentication;

public class GoogleAuthentication : IExternalAuthenticationProvider
{
    public void Add(IServiceCollection servicesCollection, AuthenticationBuilder authenticationBuilder, IConfiguration configuration)
    {
        var googleId = configuration.GetValue<string>("BlogFodder:Identity:ExternalProviders:Google:ClientId");
        if (!googleId.IsNullOrWhiteSpace())
        {
            // https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-5.0
            authenticationBuilder.AddGoogle(options =>
            {
                options.ClientId = googleId;
                options.ClientSecret = configuration.GetValue<string>("BlogFodder:Identity:ExternalProviders:Google:ClientSecret") ?? "";
            });
        }
    }
}