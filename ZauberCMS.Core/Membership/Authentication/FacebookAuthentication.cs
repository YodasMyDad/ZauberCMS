using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Plugins.Interfaces;

namespace ZauberCMS.Core.Membership.Authentication;

public class FacebookAuthentication : IExternalAuthenticationProvider
{
    public void Add(IServiceCollection servicesCollection, AuthenticationBuilder authenticationBuilder,
        IConfiguration configuration)
    {
        var facebookId = configuration.GetValue<string>("Zauber:Identity:ExternalProviders:Facebook:AppId");
        if (!facebookId.IsNullOrWhiteSpace())
        {
            // https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/facebook-logins?view=aspnetcore-5.0
            authenticationBuilder.AddFacebook(facebookOptions =>
            {
                facebookOptions.AppId = facebookId;
                facebookOptions.AppSecret =
                    configuration.GetValue<string>("Zauber:Identity:ExternalProviders:Facebook:AppSecret") ?? "";
            });
        }
    }
}