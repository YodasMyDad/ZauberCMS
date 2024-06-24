using AutoMapper;

namespace ZauberCMS.Core.Audit.Mapping;

public class AuditMapper : Profile
{
    public AuditMapper()
    {
        CreateMap<Models.Audit, Models.Audit>();
    }
}