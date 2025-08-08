using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class CreateUpdateUserParameters
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? NewPasswordConfirmation { get; set; }
    public User User { get; set; } = new();
    public IBrowserFile? ProfileImageUpload { get; set; }
    public string? Password { get; set; }
    public List<string>? Roles { get; set; }
}