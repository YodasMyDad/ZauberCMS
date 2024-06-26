using MediatR;
using ZauberCMS.Core.Data.Models;

namespace ZauberCMS.Core.Data.Commands;

public class GetGlobalDataCommand : IRequest<GlobalData?>
{
    public string? Alias { get; set; }
}