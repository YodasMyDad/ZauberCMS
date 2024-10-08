using AutoMapper;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

public class UserMapper : Profile
{
    public UserMapper()
    {
        CreateMap<User, User>()
            .ForMember(x => x.UserRoles, opt => opt.Ignore())
            .ForMember(x => x.Audits, opt => opt.Ignore())
            .ForMember(x => x.PropertyData, opt => opt.Ignore())
            ;
    }
}