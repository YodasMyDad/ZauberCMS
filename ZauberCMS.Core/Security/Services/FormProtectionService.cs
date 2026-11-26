using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Security.Interfaces;
using ZauberCMS.Core.Security.Models;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Security.Services;

/// <inheritdoc />
public class FormProtectionService(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ZauberSettings> settings,
    TimeProvider? timeProvider = null) : IFormProtectionService
{
    /// <summary>A form left open longer than this is shown again instead of being accepted.</summary>
    public static readonly TimeSpan MaximumAge = TimeSpan.FromHours(12);

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ZauberCMS.FormProtection.v1");
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public string TokenFieldName => "zauber_form_token";

    // Not a name browsers or password managers fill in for people
    public string HoneypotFieldName => "contact_subject_line";

    public string CreateToken() =>
        _protector.Protect(_timeProvider.GetUtcNow().UtcTicks.ToString(CultureInfo.InvariantCulture));

    public FormProtectionResult Validate(HttpContext httpContext)
    {
        if (!httpContext.Request.HasFormContentType)
        {
            return FormProtectionResult.Expired;
        }

        var form = httpContext.Request.Form;
        return Validate(form[TokenFieldName].ToString(), form[HoneypotFieldName].ToString());
    }

    public FormProtectionResult Validate(string? token, string? honeypotValue)
    {
        if (!string.IsNullOrEmpty(honeypotValue))
        {
            return FormProtectionResult.HoneypotFilled;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return FormProtectionResult.Expired;
        }

        long ticks;
        try
        {
            if (!long.TryParse(_protector.Unprotect(token), NumberStyles.None, CultureInfo.InvariantCulture, out ticks))
            {
                return FormProtectionResult.Expired;
            }
        }
        catch (CryptographicException)
        {
            // Not ours, or issued before the site's keys changed
            return FormProtectionResult.Expired;
        }

        var age = _timeProvider.GetUtcNow() - new DateTimeOffset(ticks, TimeSpan.Zero);
        if (age > MaximumAge || age < TimeSpan.Zero)
        {
            return FormProtectionResult.Expired;
        }

        var minimum = TimeSpan.FromSeconds(Math.Max(0, settings.Value.FormProtection.MinimumFillSeconds));
        return age < minimum ? FormProtectionResult.TooFast : FormProtectionResult.Passed;
    }
}
