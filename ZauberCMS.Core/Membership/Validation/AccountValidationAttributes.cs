using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;
using ZauberCMS.Core.Localization;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Validation;

// The account pages' DataAnnotations messages in the reader's language (Roadmap D92; Docs/AdminUI.md "Localisation",
// rule 8). Blazor's DataAnnotationsValidator gives the ValidationContext the services of the request or circuit, so
// these attributes read IStringLocalizer<ZauberText> from it; validated without services (Validator called with
// none), each gives its base attribute's English message. The field's name is localised for ZauberCMS's own account
// fields, from the member's name, and for the navigation editor's item (NavigationItem); any other member keeps its
// display name.

/// <summary>What the account attributes share: the localiser, the field's name and the failed result.</summary>
internal static class AccountValidationText
{
    /// <summary>The localiser in the validation's services; null when it was given none.</summary>
    internal static IStringLocalizer<ZauberText>? Text(ValidationContext context) =>
        context.GetService(typeof(IStringLocalizer<ZauberText>)) as IStringLocalizer<ZauberText>;

    /// <summary>
    /// The field's name for a message: a <see cref="NavigationItem"/>'s name and address, ZauberCMS's account fields
    /// (<see cref="RegisterUserParameters"/>, <see cref="LoginUserParameters"/>, <see cref="ForgotPasswordParameters"/>,
    /// <see cref="ResetPasswordParameters"/>) by member name, any other member by its display name.
    /// </summary>
    internal static string FieldName(IStringLocalizer<ZauberText> text, ValidationContext context) =>
        context.ObjectInstance is NavigationItem
            ? context.MemberName switch
            {
                nameof(NavigationItem.Name) => text["shared_navigationItemName"].Value,
                nameof(NavigationItem.Url) => text["shared_navigationItemUrl"].Value,
                _ => context.DisplayName
            }
            : context.MemberName switch
            {
                nameof(RegisterUserParameters.Email) => text["membership_fieldEmail"].Value,
                nameof(RegisterUserParameters.Password) => text["membership_fieldPassword"].Value,
                nameof(RegisterUserParameters.Username) => text["membership_fieldUsername"].Value,
                nameof(ResetPasswordParameters.Code) => text["membership_fieldCode"].Value,
                _ => context.DisplayName
            };

    /// <summary>A failed check with <paramref name="message"/>, for the member being validated when there is one.</summary>
    internal static ValidationResult Failed(string message, ValidationContext context) =>
        new(message, context.MemberName is { } member ? [member] : null);
}

/// <summary>
/// <see cref="RequiredAttribute"/> with its message in the reader's language (<c>membership_fieldRequired</c>,
/// "The {0} field is required.").
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class ZauberRequiredAttribute : RequiredAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var result = base.IsValid(value, validationContext);
        var text = AccountValidationText.Text(validationContext);
        return result == ValidationResult.Success || text == null
            ? result
            : AccountValidationText.Failed(
                text["membership_fieldRequired", AccountValidationText.FieldName(text, validationContext)],
                validationContext);
    }
}

/// <summary>
/// <see cref="EmailAddressAttribute"/>'s check (that class is sealed: this is a <see cref="DataTypeAttribute"/> for
/// <see cref="DataType.EmailAddress"/>, as it is) with its message in the reader's language
/// (<c>membership_fieldInvalidEmail</c>, "The {0} field is not a valid e-mail address.").
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class ZauberEmailAddressAttribute : DataTypeAttribute
{
    private static readonly EmailAddressAttribute EmailCheck = new();

    public ZauberEmailAddressAttribute() : base(DataType.EmailAddress)
    {
    }

    public override bool IsValid(object? value) => EmailCheck.IsValid(value);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (IsValid(value))
        {
            return ValidationResult.Success;
        }

        var text = AccountValidationText.Text(validationContext);
        return AccountValidationText.Failed(text == null
            ? EmailCheck.FormatErrorMessage(validationContext.DisplayName)
            : text["membership_fieldInvalidEmail", AccountValidationText.FieldName(text, validationContext)].Value,
            validationContext);
    }
}

/// <summary>
/// <see cref="StringLengthAttribute"/> with its message in the reader's language: with a
/// <see cref="StringLengthAttribute.MinimumLength"/>, the account pages' own sentence
/// (<c>membership_fieldLengthBetween</c>, "The {0} must be at least {1} and at max {2} characters long."), else .NET's
/// (<c>membership_fieldMaxLength</c>). Without services, the base message: the usage's <c>ErrorMessage</c>, if set.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
internal sealed class ZauberStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var result = base.IsValid(value, validationContext);
        var text = AccountValidationText.Text(validationContext);
        if (result == ValidationResult.Success || text == null)
        {
            return result;
        }

        var field = AccountValidationText.FieldName(text, validationContext);
        return AccountValidationText.Failed(MinimumLength > 0
            ? text["membership_fieldLengthBetween", field, MinimumLength, MaximumLength].Value
            : text["membership_fieldMaxLength", field, MaximumLength].Value, validationContext);
    }
}

/// <summary>
/// <see cref="CompareAttribute"/> for a password's confirmation box, with its message in the reader's language
/// (<c>membership_passwordsDoNotMatch</c>, "The password and confirmation password do not match."). Without
/// services, the base message: the usage's <c>ErrorMessage</c>, if set.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class ZauberComparePasswordAttribute(string otherProperty) : CompareAttribute(otherProperty)
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var result = base.IsValid(value, validationContext);
        var text = AccountValidationText.Text(validationContext);
        return result == ValidationResult.Success || text == null
            ? result
            : AccountValidationText.Failed(text["membership_passwordsDoNotMatch"], validationContext);
    }
}
