using MediatR;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Commands;

public class DeleteLanguageDictionaryCommand : IRequest<HandlerResult<LanguageDictionary?>>
{
    public Guid? Id { get; set; }
}