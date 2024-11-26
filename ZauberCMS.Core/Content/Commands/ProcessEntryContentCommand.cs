using MediatR;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Commands;

public class ProcessEntryContentCommand : IRequest<EntryContentResult?>
{
    public string? FullUrl { get; init; }
    public string? Slug { get; init; }
    public bool IsRootContent { get; init; }
}