using AutoMapper;
using ZauberCMS.Core.Seo.Models;

namespace ZauberCMS.Core.Seo.Mapping;

public class SeoRedirectMapper : Profile
{
    public SeoRedirectMapper()
    {
        CreateMap<SeoRedirect, SeoRedirect>()
            .ForMember(x => x.Domain, opt => opt.Ignore());
    }
}