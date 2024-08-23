using AutoMapper;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Mapping;

public class LanguageTextMapper : Profile
{
    public LanguageTextMapper()
    {
        CreateMap<LanguageText, LanguageText>()
            .ForMember(x => x.LanguageDictionary, opt => opt.Ignore())
            .ForMember(x => x.Language, opt => opt.Ignore())
            ;
    }
}