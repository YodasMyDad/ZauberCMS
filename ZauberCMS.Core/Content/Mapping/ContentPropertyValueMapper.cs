using AutoMapper;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentPropertyValueMapper : Profile
{
    public ContentPropertyValueMapper()
    {
        CreateMap<ContentPropertyValue, ContentPropertyValue>()
            .ForMember(x => x.Content, opt => opt.Ignore())
            ;
    }
}