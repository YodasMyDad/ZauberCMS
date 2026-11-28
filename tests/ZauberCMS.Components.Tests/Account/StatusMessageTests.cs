using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Components.Account.Shared;
using ZauberCMS.Components.Tests.Shared;
using ZauberCMS.Core.Membership;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Shared.Services;

namespace ZauberCMS.Components.Tests.Account;

/// <summary>
/// ZA-280: an account page's status message was an error only when its text started with the English word "Error",
/// so a translated error showed as a success. The type now travels with the message.
/// </summary>
public class StatusMessageTests
{
    [Fact]
    public void A_status_with_a_type_writes_the_type_beside_the_message()
    {
        var context = new DefaultHttpContext();
        var manager = new IdentityRedirectManager(new TestNavigationManager());

        // NavigateTo throws outside static rendering; the cookies are written before it
        Assert.Throws<InvalidOperationException>(() =>
            manager.RedirectToWithStatus("Account/Login", "Erreur du fournisseur", ResultMessageType.Error, context));

        var cookies = context.Response.Headers.SetCookie.ToString();
        Assert.Contains($"{IdentityRedirectManager.StatusTypeCookieName}=Error", cookies, StringComparison.Ordinal);
        Assert.Contains($"{IdentityRedirectManager.StatusCookieName}=", cookies, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Erreur%20du%20fournisseur", "Error", "alert-danger")] // a translation: the type decides
    [InlineData("Unable%20to%20login", "Error", "alert-danger")]
    [InlineData("Error%20loading", null, "alert-danger")] // written without a type: the template's rule
    [InlineData("Saved", null, "alert-success")]
    [InlineData("Saved", "Warning", "alert-warning")]
    public async Task The_message_from_the_cookie_is_shown_as_its_type(string message, string? type, string expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{IdentityRedirectManager.StatusCookieName}={message}" +
                                         (type is null ? string.Empty : $"; {IdentityRedirectManager.StatusTypeCookieName}={type}");

        var html = await RenderAsync(context, new Dictionary<string, object>());

        Assert.Contains(expected, html, StringComparison.Ordinal);
        Assert.Contains(Uri.UnescapeDataString(message), html, StringComparison.Ordinal);
        // Both are read once
        var cleared = context.Response.Headers.SetCookie.ToString();
        Assert.Contains(IdentityRedirectManager.StatusCookieName + "=", cleared, StringComparison.Ordinal);
        if (type != null)
        {
            Assert.Contains(IdentityRedirectManager.StatusTypeCookieName + "=", cleared, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_message_given_with_a_type_is_shown_as_that_type()
    {
        var html = await RenderAsync(new DefaultHttpContext(), new Dictionary<string, object>
        {
            [nameof(StatusMessage.Message)] = "Nous n'avons pas pu vous connecter",
            [nameof(StatusMessage.MessageType)] = ResultMessageType.Error
        });

        Assert.Contains("alert-danger", html, StringComparison.Ordinal);
    }

    private static Task<string> RenderAsync(HttpContext context, Dictionary<string, object> parameters) =>
        AdminRenderer.RenderAsync<WithHttpContext>(
            services => services.AddSingleton<IHtmlSanitizerService>(new DefaultHtmlSanitizerService()),
            new Dictionary<string, object?> { [nameof(WithHttpContext.Context)] = context, [nameof(WithHttpContext.Parameters)] = parameters });

    // The account pages give StatusMessage the request as a cascading value
    private sealed class WithHttpContext : ComponentBase
    {
        [Parameter] public HttpContext Context { get; set; } = null!;
        [Parameter] public Dictionary<string, object> Parameters { get; set; } = [];

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<HttpContext>>(0);
            builder.AddComponentParameter(1, nameof(CascadingValue<HttpContext>.Value), Context);
            builder.AddComponentParameter(2, nameof(CascadingValue<HttpContext>.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent<StatusMessage>(0);
                inner.AddMultipleAttributes(1, Parameters);
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://localhost/", "https://localhost/Account/ExternalLogin");

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
        }
    }
}
