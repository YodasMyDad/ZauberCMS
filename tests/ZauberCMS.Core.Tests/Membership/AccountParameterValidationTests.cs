using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Parameters;

namespace ZauberCMS.Core.Tests.Membership;

/// <summary>
/// Roadmap Phase 10h-4 (D92): the account pages' DataAnnotations messages come from ZauberCMS's text, which
/// Blazor's <c>DataAnnotationsValidator</c> reaches through the <see cref="ValidationContext"/>'s services. The
/// English is the same as .NET's and the parameter classes' own messages, with services and without them.
/// </summary>
public class AccountParameterValidationTests
{
    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddLogging()
        .AddZauberText([typeof(ZauberText).Assembly], [typeof(ZauberText).Assembly])
        .BuildServiceProvider();

    private static List<ValidationResult> Validate(object model, IServiceProvider? services)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model, services, null), results, true);
        return results;
    }

    private static void AssertMessages(IEnumerable<ValidationResult> results, params (string Member, string Message)[] expected)
    {
        var actual = results.Select(r => (Member: Assert.Single(r.MemberNames), Message: r.ErrorMessage!))
            .OrderBy(r => r.Member, StringComparer.Ordinal).ToList();
        Assert.Equal(expected.OrderBy(e => e.Member, StringComparer.Ordinal).ToList(), actual);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void D92_registering_with_empty_fields_names_each_field_in_English(bool withServices)
    {
        var results = Validate(new RegisterUserParameters { Username = "", Email = "", Password = "" },
            withServices ? Services : null);

        AssertMessages(results,
            ("Email", "The Email field is required."),
            ("Password", "The Password field is required."),
            ("Username", "The Username field is required."));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void D92_registering_with_wrong_values_gives_the_length_and_address_messages(bool withServices)
    {
        var results = Validate(new RegisterUserParameters
        {
            Username = new string('u', 151),
            Email = "not-an-address",
            Password = "short"
        }, withServices ? Services : null);

        AssertMessages(results,
            ("Email", "The Email field is not a valid e-mail address."),
            ("Password", "The Password must be at least 8 and at max 100 characters long."),
            ("Username", "The field Username must be a string with a maximum length of 150."));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void D92_resetting_needs_the_code_and_a_matching_confirmation(bool withServices)
    {
        var results = Validate(new ResetPasswordParameters
        {
            Email = "someone@example.test",
            Password = "Passw0rd-For-Tests",
            ConfirmPassword = "Something-Else-1"
        }, withServices ? Services : null);

        AssertMessages(results,
            ("Code", "The Code field is required."),
            ("ConfirmPassword", "The password and confirmation password do not match."));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void D92_signing_in_and_forgetting_check_the_address(bool withServices)
    {
        var services = withServices ? Services : null;

        AssertMessages(Validate(new LoginUserParameters { Email = "", Password = "" }, services),
            ("Email", "The Email field is required."),
            ("Password", "The Password field is required."));
        AssertMessages(Validate(new ForgotPasswordParameters { Email = "not-an-address" }, services),
            ("Email", "The Email field is not a valid e-mail address."));
        Assert.Empty(Validate(new LoginUserParameters { Email = "someone@example.test", Password = "x" }, services));
    }

    [Fact]
    public void D92_the_messages_and_field_names_come_from_the_localiser_in_the_validation_services()
    {
        using var services = new ServiceCollection()
            .AddSingleton<IStringLocalizer<ZauberText>, KeyLocalizer>()
            .BuildServiceProvider();

        AssertMessages(Validate(new RegisterUserParameters { Username = "", Email = "bad", Password = "short" }, services),
            ("Email", "membership_fieldInvalidEmail(membership_fieldEmail)"),
            ("Password", "membership_fieldLengthBetween(membership_fieldPassword|8|100)"),
            ("Username", "membership_fieldRequired(membership_fieldUsername)"));
        AssertMessages(Validate(new ResetPasswordParameters
            {
                Email = "someone@example.test", Password = "Passw0rd-For-Tests", ConfirmPassword = "x", Code = "c"
            }, services),
            ("ConfirmPassword", "membership_passwordsDoNotMatch"));
    }

    /// <summary>Shows the key and its arguments, so a test sees which text was asked for.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<ZauberText>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, $"{name}({string.Join("|", arguments)})");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
