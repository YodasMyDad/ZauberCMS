using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Security.Interfaces;
using ZauberCMS.Core.Security.Models;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Security.Services;

/// <inheritdoc />
/// <remarks>
/// The token is a signed time and a random id. Used tokens are remembered in <see cref="IMemoryCache"/> until they
/// would have expired anyway (ZA-150), so each server refuses a token it has seen used; with several servers behind a
/// load balancer a token could be used once on each.
/// </remarks>
public class FormProtectionService(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<ZauberSettings> settings,
    TimeProvider? timeProvider = null,
    IMemoryCache? usedTokens = null) : IFormProtectionService
{
    /// <summary>A form left open longer than this is shown again instead of being accepted.</summary>
    public static readonly TimeSpan MaximumAge = TimeSpan.FromHours(12);

    private static readonly Lock UseLock = new();

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ZauberCMS.FormProtection.v1");
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public string TokenFieldName => "zauber_form_token";

    // Not a name browsers or password managers fill in for people
    public string HoneypotFieldName => "contact_subject_line";

    public string CreateToken() =>
        _protector.Protect(_timeProvider.GetUtcNow().UtcTicks.ToString(CultureInfo.InvariantCulture) + "." +
                           Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

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

        if (Read(token) is not var (issued, payload))
        {
            return FormProtectionResult.Expired;
        }

        var age = _timeProvider.GetUtcNow() - issued;
        if (age > MaximumAge || age < TimeSpan.Zero)
        {
            return FormProtectionResult.Expired;
        }

        if (usedTokens != null && usedTokens.TryGetValue(UsedKey(payload), out _))
        {
            return FormProtectionResult.AlreadyUsed;
        }

        var minimum = TimeSpan.FromSeconds(Math.Max(0, settings.Value.FormProtection.MinimumFillSeconds));
        return age < minimum ? FormProtectionResult.TooFast : FormProtectionResult.Passed;
    }

    public bool TryUseToken(string? token)
    {
        if (Read(token) is not var (issued, payload))
        {
            return false;
        }

        if (usedTokens == null)
        {
            return true;
        }

        // Kept until the token would be refused as too old anyway
        var remaining = issued + MaximumAge - _timeProvider.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        var key = UsedKey(payload);
        lock (UseLock)
        {
            if (usedTokens.TryGetValue(key, out _))
            {
                return false;
            }

            usedTokens.Set(key, true, remaining);
            return true;
        }
    }

    /// <summary>
    /// When the token was issued and what it holds, or null when it is not one of this site's tokens. Used tokens are
    /// remembered by what they hold, not by the text sent: the same token with spaces added decodes to the same
    /// value and must count as the same token.
    /// </summary>
    private (DateTimeOffset Issued, string Payload)? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        string value;
        try
        {
            value = _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            // Not ours, or issued before the site's keys changed
            return null;
        }

        // "ticks.id"; a token issued before the id was added is the ticks alone
        var ticksText = value.Split('.', 2)[0];
        return long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) &&
               ticks >= DateTimeOffset.MinValue.UtcTicks && ticks <= DateTimeOffset.MaxValue.UtcTicks
            ? (new DateTimeOffset(ticks, TimeSpan.Zero), value)
            : null;
    }

    private static string UsedKey(string payload) =>
        "ZauberCMS.FormToken." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
}
