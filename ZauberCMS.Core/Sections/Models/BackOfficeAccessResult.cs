namespace ZauberCMS.Core.Sections.Models;

/// <summary>The answer of an <c>IBackOfficeAccessService</c> check: allowed, or refused with the reason to show.</summary>
public sealed class BackOfficeAccessResult
{
    private BackOfficeAccessResult(bool allowed, string? reason)
    {
        Allowed = allowed;
        Reason = reason;
    }

    /// <summary>True when the change may go ahead.</summary>
    public bool Allowed { get; }

    /// <summary>Why it may not, in words for the person who asked; null when allowed.</summary>
    public string? Reason { get; }

    /// <summary>Allowed.</summary>
    public static BackOfficeAccessResult Allow { get; } = new(true, null);

    /// <summary>Refused, for <paramref name="reason"/>.</summary>
    public static BackOfficeAccessResult Deny(string reason) => new(false, reason);
}
