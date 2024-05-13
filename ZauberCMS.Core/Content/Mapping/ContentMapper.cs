using AutoMapper;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentMapper : Profile
{
    public ContentMapper()
    {
        CreateMap<Models.Content, Models.Content>()
            .ForMember(x => x.ContentType, opt => opt.Ignore());
    }
}