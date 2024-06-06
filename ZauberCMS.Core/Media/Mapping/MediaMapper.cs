using AutoMapper;

namespace ZauberCMS.Core.Media.Mapping;

public class MediaMapper : Profile
{
    public MediaMapper()
    {
        CreateMap<Models.Media, Models.Media>()
            .ForMember(x => x.Children, opt => opt.Ignore())
            .ForMember(x => x.Parent, opt => opt.Ignore());
    }
}