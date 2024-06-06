using AutoMapper;

namespace ZauberCMS.Core.Media.Mapping;

public class MediaMapper : Profile
{
    public MediaMapper()
    {
        CreateMap<Models.Media, Models.Media>();
    }
}