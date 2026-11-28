using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZauberCMS.Components.Admin.Layout;
using ZauberCMS.Components.Shared;
using ZauberCMS.Core.Sections.Interfaces;
using ZauberCMS.Core.Shared;

namespace ZauberCMS.Components.Tests.Shared;

/// <summary>
/// The section checks read the roles on a cache miss; a read that fails shows where the check is, with a way to
/// try again, instead of ending the circuit (ZA-215, Roadmap D32).
/// </summary>
public class SectionChecksInBoundaryTests
{
    [Fact]
    public async Task ZA215_a_failed_section_check_in_the_header_shows_in_its_place_and_is_logged()
    {
        var log = new RecordingLoggerProvider();

        var html = await AdminRenderer.RenderAsync<SectionLinks>(services =>
        {
            services.AddSingleton<ILoggerProvider>(log);
            services.AddSingleton(Fake<IBackOfficeAccessService>.Create((_, _) =>
                Task.FromException<IReadOnlyList<ISection>>(new InvalidOperationException("Database is down"))));
            services.AddSingleton<AuthenticationStateProvider>(new SignedIn());
            services.AddSingleton<TreeState>();
        });

        Assert.Contains("The sections could not be shown: try again", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.DoesNotContain("Database is down", html);
        Assert.Contains(log.Entries, e => e.Level == LogLevel.Error && e.Exception?.Message == "Database is down");
    }

    [Fact]
    public async Task ZA215_the_header_lists_the_sections_the_check_gives()
    {
        var html = await AdminRenderer.RenderAsync<SectionLinks>(services =>
        {
            services.AddSingleton(Fake<IBackOfficeAccessService>.Create((_, _) =>
                Task.FromResult<IReadOnlyList<ISection>>([new TestSection()])));
            services.AddSingleton<AuthenticationStateProvider>(new SignedIn());
            services.AddSingleton<TreeState>();
        });

        Assert.Contains("Test Section", html);
        Assert.DoesNotContain("could not be shown", html);
    }

    [Fact]
    public async Task ZA215_a_section_policy_that_throws_inside_an_admin_error_boundary_shows_in_it()
    {
        // What Routes.razor relies on for the admin: its AuthorizeView and AuthorizeRouteView run the section
        // policies, and the AdminErrorBoundary round them catches a check that throws
        var caught = new List<Exception>();

        var html = await AdminRenderer.RenderAsync<AdminErrorBoundary>(services =>
        {
            services.AddAuthorizationCore(options => options.AddPolicy("Section", policy =>
                policy.AddRequirements(new ThrowingRequirement())));
            services.AddSingleton<IAuthorizationHandler, ThrowingRequirementHandler>();
            services.AddSingleton<AuthenticationStateProvider>(new SignedIn());
        }, new Dictionary<string, object?>
        {
            [nameof(AdminErrorBoundary.Name)] = "This page",
            [nameof(AdminErrorBoundary.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<CascadingAuthenticationState>(0);
                builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<AuthorizeView>(0);
                    inner.AddAttribute(1, nameof(AuthorizeView.Policy), "Section");
                    inner.AddAttribute(2, nameof(AuthorizeView.ChildContent),
                        (RenderFragment<AuthenticationState>)(_ => content => content.AddContent(0, "the page")));
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            })
        }, caught);

        Assert.Contains("This page could not be shown", html);
        Assert.Contains("Try again", html);
        Assert.DoesNotContain("the page", html);
        Assert.Equal("Roles could not be read", Assert.Single(caught).Message);
    }

    private sealed class SignedIn : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "editor")], "Test"))));
    }

    private sealed class TestSection : ISection
    {
        public string Name => "Test Section";
        public string Alias => "TestSection";
        public string IndexUrl => "/admin/test";
        public int SortOrder => 1;
    }

    private sealed class ThrowingRequirement : IAuthorizationRequirement;

    private sealed class ThrowingRequirementHandler : AuthorizationHandler<ThrowingRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            ThrowingRequirement requirement) => throw new InvalidOperationException("Roles could not be read");
    }
}

/// <summary>An interface whose every call is answered by <c>handler</c>.</summary>
public class Fake<T> : DispatchProxy where T : class
{
    private Func<MethodInfo, object?[]?, object?> _handler = (_, _) => null;

    public static T Create(Func<MethodInfo, object?[]?, object?> handler)
    {
        var proxy = Create<T, Fake<T>>();
        ((Fake<T>)(object)proxy)._handler = handler;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _handler(targetMethod!, args);
}
