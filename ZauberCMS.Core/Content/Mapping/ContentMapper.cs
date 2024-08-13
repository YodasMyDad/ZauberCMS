using AutoMapper;

namespace ZauberCMS.Core.Content.Mapping;

public class ContentMapper : Profile
{
    public ContentMapper()
    {
        CreateMap<Models.Content, Models.Content>()
            .ForMember(x => x.ContentType, opt => opt.Ignore())
            .ForMember(x => x.Children, opt => opt.Ignore())
            .ForMember(x => x.Parent, opt => opt.Ignore())
            .ForMember(x => x.PropertyData, opt => opt.Ignore())
            .ForMember(x => x.LastUpdatedBy, opt => opt.Ignore())
            .ForMember(x => x.UnpublishedContent, opt => opt.Ignore())
            .ForMember(x => x.InternalRedirectIdAsString, opt => opt.Ignore());
    }
}