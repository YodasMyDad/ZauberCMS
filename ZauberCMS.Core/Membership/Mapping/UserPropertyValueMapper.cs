using AutoMapper;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Membership.Mapping;

public class UserPropertyValueMapper : Profile
{
    public UserPropertyValueMapper()
    {
        CreateMap<UserPropertyValue, UserPropertyValue>()
            .ForMember(x => x.User, opt => opt.Ignore())
            ;
    }
}