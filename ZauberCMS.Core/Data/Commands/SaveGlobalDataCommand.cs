using MediatR;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Data.Commands;

public class SaveGlobalDataCommand : IRequest<HandlerResult<GlobalData>>
{
    public GlobalData? GlobalData { get; set; }
}