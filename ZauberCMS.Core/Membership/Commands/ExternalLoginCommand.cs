using MediatR;
using Microsoft.AspNetCore.Identity;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class ExternalLoginCommand : IRequest<AuthenticationResult>
{
    public string? ProviderDisplayName { get; set; }

    public ExternalLoginInfo? ExternalLoginInfo { get;set; }

    public string? ReturnUrl { get; set; }
}