using MediatR;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Data.Commands;

public class SaveGlobalDataCommand : IRequest<HandlerResult<GlobalData>>
{
    public string? Alias { get; set; }
    public string? Data { get; set; }
}