using AutoMapper;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentTypeMapper : Profile
{
    public ContentTypeMapper()
    {
        CreateMap<ContentType, ContentType>()
            .ForMember(x => x.LinkedContent, opt => opt.Ignore());
    }
}