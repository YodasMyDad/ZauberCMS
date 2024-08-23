using AutoMapper;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Mapping;

public class LanguageDictionaryMapper : Profile
{
    public LanguageDictionaryMapper()
    {
        CreateMap<LanguageDictionary, LanguageDictionary>()
            .ForMember(x => x.Texts, opt => opt.Ignore())
            ;
    }
}