using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Audit.Commands;

public class CleanupOldAuditsCommand : IRequest<HandlerResult<int>>
{
    public int DaysToKeep { get; set; } = 90;
}