using Microsoft.AspNetCore.Authorization;

namespace ZauberCMS.Core.Membership.Models
{
    public abstract class Privilege<T>
    {
        private readonly IAuthorizationService _authorizationService;

        protected Privilege(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
        }

        public abstract bool CanCreate(T item, User user);
        public abstract bool CanEdit(T item, User user);
    }
}