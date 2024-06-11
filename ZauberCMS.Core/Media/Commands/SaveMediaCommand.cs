using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Commands;

public class SaveMediaCommand : IRequest<List<FileSaveResult>>
{
    public List<FileSaveResult> FilesToSave { get; set; } = [];
    public List<Models.Media> MediaToSave { get; set; } = [];
    public Guid? ParentFolderId { get; set; }
}