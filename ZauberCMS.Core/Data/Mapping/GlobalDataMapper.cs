using AutoMapper;
using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Data.Mapping;

public class GlobalDataMapper : Profile
{
    public GlobalDataMapper()
    {
        CreateMap<GlobalData, GlobalData>()
            ;
    }
}