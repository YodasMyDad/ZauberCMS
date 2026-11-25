using System;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class DeleteUserParameters
{
    /// <summary>Id of the user to delete.</summary>
    public Guid Id { get; set; }

    /// <summary>Old name for <see cref="Id"/>. Still honoured when <see cref="Id"/> is empty.</summary>
    [Obsolete("Use Id. UserId will be removed in the next major version.")]
    public Guid UserId { get; set; }
}