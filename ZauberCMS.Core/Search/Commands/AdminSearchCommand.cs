using MediatR;
using ZauberCMS.Core.Search.Models;

namespace ZauberCMS.Core.Search.Commands;

public class AdminSearchCommand(string searchTerm) : IRequest<List<AdminSearchResult>>
{
    public string SearchTerm { get; } = searchTerm;
    public int Amount { get; } = 30;
}