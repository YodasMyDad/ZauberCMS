using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class CreateUpdateUserCommand : IRequest<HandlerResult<User>>
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? NewPasswordConfirmation { get; set; }
    public User User { get; set; } = new();
    public IBrowserFile? ProfileImageUpload { get; set; }
}