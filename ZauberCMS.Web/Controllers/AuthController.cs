using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    UserManager<User> userManager,
    SignInManager<User> signInManager)
    : ControllerBase
{
    [HttpGet("refreshsignin")]
    public async Task<IActionResult> RefreshSignIn(string? redirectUrl = null)
    {
        var user = await userManager.GetUserAsync(User);
        if (user != null) await signInManager.RefreshSignInAsync(user);
        return Redirect(redirectUrl ?? "/");
    }
    
    [HttpGet("logout")]
    public async Task<IActionResult> Logout(string? redirectUrl = null)
    {
        await signInManager.SignOutAsync();
        return Redirect(redirectUrl ?? "/"); // Redirect to the home page after logout
    }
}