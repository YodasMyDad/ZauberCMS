using Microsoft.AspNetCore.Authorization;

namespace ZauberCMS.Core.Membership.Models;

public abstract class Privilege<T>(IAuthorizationService authorizationService)
{
    public abstract bool CanCreate(T item, User user);
    public abstract bool CanEdit(T item, User user);
}