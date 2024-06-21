using AutoMapper;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

public class RoleMapper : Profile
{
    public RoleMapper()
    {
        CreateMap<Role, Role>()
            .ForMember(x => x.UserRoles, opt => opt.Ignore())
            ;
    }
}