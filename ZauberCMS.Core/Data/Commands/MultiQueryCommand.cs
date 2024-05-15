using MediatR;
using ZauberCMS.Core.Data.Interfaces;

namespace ZauberCMS.Core.Data.Commands;

public class MultiQueryCommand(List<IQueryModel> queries) : IRequest<Dictionary<string, IEnumerable<object>>>
{
    public List<IQueryModel> Queries { get; set; } = queries;
}