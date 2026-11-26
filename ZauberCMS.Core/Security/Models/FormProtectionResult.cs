namespace ZauberCMS.Core.Security.Models;

/// <summary>What <see cref="Interfaces.IFormProtectionService"/> made of a submitted form.</summary>
public enum FormProtectionResult
{
    /// <summary>Nothing suggests a script filled the form in.</summary>
    Passed,

    /// <summary>The field people cannot see had a value: only a script fills it.</summary>
    HoneypotFilled,

    /// <summary>The form came back faster than a person can fill it in.</summary>
    TooFast,

    /// <summary>The token is missing, was not issued by this site, or is too old. Show the form again.</summary>
    Expired
}
