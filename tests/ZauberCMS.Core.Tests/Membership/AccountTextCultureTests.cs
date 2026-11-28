using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Membership.Validation;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tests.Infrastructure;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// Roadmap Phase 10h-4 (D92, D95, D131): the account pages' messages built in Core (the DataAnnotations messages
/// through Blazor's validator, ASP.NET Identity's own messages) are ZauberCMS's text in the culture of the code that
/// asks, which on an account page is the request's. This test assembly plays a site with a French
/// <c>ZauberText/Membership.fr.resx</c>.
/// </summary>
public class AccountTextCultureTests
{
    private static readonly Assembly Site = typeof(AccountTextCultureTests).Assembly;
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    [Fact]
    public async Task D131_Identity_s_messages_and_the_account_forms_follow_the_culture_through_the_bootstrap()
    {
        var contentRoot = TestSite.CreateContentRoot();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: true,
                additionalAssemblies: [Site]);
            using var scope = app.Services.CreateScope();
            var describer = scope.ServiceProvider.GetRequiredService<IdentityErrorDescriber>();
            Assert.IsType<ZauberIdentityErrorDescriber>(describer);

            CultureInfo.CurrentUICulture = French;
            var tooShort = describer.PasswordTooShort(8);
            Assert.Equal("Les mots de passe doivent comporter au moins 8 caractères.", tooShort.Description);
            Assert.Equal(nameof(IdentityErrorDescriber.PasswordTooShort), tooShort.Code);
            // A message the site does not translate is ZauberCMS's English
            Assert.Equal("Passwords must have at least one digit ('0'-'9').", describer.PasswordRequiresDigit().Description);
            Assert.Equal(["Le champ Adresse e-mail est obligatoire."], BlazorValidationMessages(scope.ServiceProvider));

            CultureInfo.CurrentUICulture = English;
            Assert.Equal("Passwords must be at least 8 characters.", describer.PasswordTooShort(8).Description);
            Assert.Equal(["The Email field is required."], BlazorValidationMessages(scope.ServiceProvider));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ZA271_the_installer_s_password_check_gives_Identity_s_messages_in_the_culture()
    {
        // The installer checks the administrator's password with the UserManager's validators before it writes
        // anything (InstallService), once the database is chosen; nothing here opens the database
        var contentRoot = TestSite.CreateContentRoot();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            await using var app = TestSite.BuildApp(TestSite.SqliteProvider,
                $"DataSource=zauber-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared", contentRoot,
                validateContainer: false, additionalAssemblies: [Site]);
            using var scope = app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var length = userManager.Options.Password.RequiredLength;

            CultureInfo.CurrentUICulture = French;
            var errors = new List<IdentityError>();
            foreach (var validator in userManager.PasswordValidators)
            {
                errors.AddRange((await validator.ValidateAsync(userManager, new User { UserName = "ada" }, "a")).Errors);
            }

            Assert.Contains(errors, e => e.Code == nameof(IdentityErrorDescriber.PasswordTooShort) &&
                                         e.Description == $"Les mots de passe doivent comporter au moins {length} caractères.");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task The_navigation_item_dialog_s_required_fields_follow_the_culture()
    {
        // NavigationItem's [ZauberRequired] (D130): the navigation editor's dialog validates it with Blazor's
        // validator, which passes the circuit's services; the field names are ZauberCMS's (shared_navigationItem*)
        var contentRoot = TestSite.CreateContentRoot();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            await using var app = TestSite.BuildApp("", "", contentRoot, validateContainer: false,
                additionalAssemblies: [Site]);
            using var scope = app.Services.CreateScope();

            CultureInfo.CurrentUICulture = English;
            Assert.Equal(["The Name field is required.", "The URL field is required."],
                NavigationItemMessages(scope.ServiceProvider));
            CultureInfo.CurrentUICulture = French;
            Assert.Equal(["Le champ Name est obligatoire.", "Le champ URL est obligatoire."],
                NavigationItemMessages(scope.ServiceProvider));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void D131_every_Identity_message_keeps_Identity_s_code_and_English()
    {
        using var services = new ServiceCollection().AddLogging()
            .AddZauberText([typeof(ZauberText).Assembly], [typeof(ZauberText).Assembly])
            .BuildServiceProvider();
        var ours = ActivatorUtilities.CreateInstance<ZauberIdentityErrorDescriber>(services);
        var identity = new IdentityErrorDescriber();
        var previous = CultureInfo.CurrentUICulture;
        var problems = new List<string>();
        try
        {
            CultureInfo.CurrentUICulture = English;
            foreach (var method in typeof(IdentityErrorDescriber).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .Where(m => m.IsVirtual && m.ReturnType == typeof(IdentityError) &&
                                     m.DeclaringType == typeof(IdentityErrorDescriber)))
            {
                var arguments = method.GetParameters()
                    .Select(p => p.ParameterType == typeof(int) ? (object)7 : "someone@example.test").ToArray();
                if (ours.GetType().GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray())
                        ?.DeclaringType != typeof(ZauberIdentityErrorDescriber))
                {
                    problems.Add($"{method.Name} is not localised");
                    continue;
                }

                var mine = (IdentityError)method.Invoke(ours, arguments)!;
                var theirs = (IdentityError)method.Invoke(identity, arguments)!;
                if (mine.Code != theirs.Code || mine.Description != theirs.Description)
                {
                    problems.Add($"{method.Name}: '{mine.Code}: {mine.Description}', Identity's '{theirs.Code}: {theirs.Description}'");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // What the login page's <DataAnnotationsValidator/> shows for an empty address: Blazor passes the page's services
    // to the validation, which is how the attributes reach the text
    private static List<string> BlazorValidationMessages(IServiceProvider services)
    {
        var model = new LoginUserParameters { Email = "", Password = "set" };
        var editContext = new EditContext(model);
        using var validation = editContext.EnableDataAnnotationsValidation(services);
        editContext.Validate();
        return editContext.GetValidationMessages().ToList();
    }

    private static List<string> NavigationItemMessages(IServiceProvider services)
    {
        var editContext = new EditContext(new NavigationItem());
        using var validation = editContext.EnableDataAnnotationsValidation(services);
        editContext.Validate();
        return editContext.GetValidationMessages().ToList();
    }
}
