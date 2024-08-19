using System.Globalization;
using MediatR;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Commands;

public class SaveLanguageCommand : IRequest<HandlerResult<Language>>
{
    public CultureInfo? CultureInfo { get; set; }
    public Guid? Id { get; set; }
}