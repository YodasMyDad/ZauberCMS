using MediatR;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Commands;

public class SaveLanguageDictionaryCommand : IRequest<HandlerResult<LanguageDictionary>>
{
    public LanguageDictionary? LanguageDictionary { get; set; }
}