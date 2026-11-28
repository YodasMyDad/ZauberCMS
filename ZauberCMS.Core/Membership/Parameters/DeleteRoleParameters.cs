using System;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Parameters;

public class DeleteRoleParameters
{
    /// <summary>Was never read: <c>DeleteRoleAsync</c> looked only at <see cref="Id"/>, so the role editor, which set
    /// this, could not delete a role (ZA-200). It is read now when <see cref="Id"/> is not set.</summary>
    [Obsolete("Use Id. Will be removed in a later major version.")]
    public Guid RoleId { get; set; }

    /// <summary>The role to delete.</summary>
    public Guid Id { get; set; }
}