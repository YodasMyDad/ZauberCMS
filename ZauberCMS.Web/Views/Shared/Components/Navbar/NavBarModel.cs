using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Web.Views.Shared.Components.Navbar;

public class NavBarModel
{
    public Content? Website { get; set; }
    public string? CurrentPath { get; set; }
}