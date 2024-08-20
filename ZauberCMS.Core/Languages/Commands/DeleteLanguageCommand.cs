using MediatR;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Commands;

public class DeleteLanguageCommand : IRequest<HandlerResult<Language?>>
{
    public Guid? Id { get; set; }
    public string? LanguageIsoCode { get; set; }
}