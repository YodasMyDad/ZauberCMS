using System.Security.Claims;
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
        var microsoftId = configuration.GetValue<string>("Zauber:Identity:ExternalProviders:Microsoft:ClientId");
        if (!microsoftId.IsNullOrWhiteSpace())
        {
            // https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-5.0
            authenticationBuilder.AddMicrosoftAccount(microsoftOptions =>
            {
                microsoftOptions.ClientId = microsoftId;
                microsoftOptions.ClientSecret = configuration.GetValue<string>("Zauber:Identity:ExternalProviders:Microsoft:ClientSecret") ?? "";
                // Microsoft does not say whether the address is verified (a tenant administrator can set any
                // value), so new accounts are only created from it when the site owner opts in.
                if (configuration.GetValue<bool>("Zauber:Identity:ExternalProviders:Microsoft:TrustEmail"))
                {
                    microsoftOptions.ClaimActions.MapCustomJson(ZauberSignInManager.EmailVerifiedClaimType,
                        ClaimValueTypes.Boolean, _ => bool.TrueString);
                }
            });
        }
    }
}