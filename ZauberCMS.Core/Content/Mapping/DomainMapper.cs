using AutoMapper;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

public class DomainMapper : Profile
{
    public DomainMapper()
    {
        CreateMap<Domain, Domain>()
            .ForMember(x => x.Language, opt => opt.Ignore());
    }
}