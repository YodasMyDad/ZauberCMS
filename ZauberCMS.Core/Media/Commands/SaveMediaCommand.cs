using MediatR;
using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Commands;

public class SaveMediaCommand : IRequest<HandlerResult<Models.Media>>
{
    public IBrowserFile? FileToSave { get; set; }
    public Models.Media? MediaToSave { get; set; }
    public Guid? ParentFolderId { get; set; }
    public bool IsUpdate { get; set; }
}