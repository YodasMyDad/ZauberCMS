using Microsoft.AspNetCore.Authorization;

namespace ZauberCMS.Core.Membership.Models;

#pragma warning disable CS9113 // Parameter is unread.
public abstract class Privilege<T>(IAuthorizationService authorizationService)
#pragma warning restore CS9113 // Parameter is unread.
{
    public abstract bool CanCreate(T item, User user);
    public abstract bool CanEdit(T item, User user);
}