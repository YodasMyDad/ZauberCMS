using Microsoft.AspNetCore.Components.Forms;

namespace ZauberCMS.Core.Media.Parameters;

public class SaveMediaParameters
{
    public IBrowserFile? FileToSave { get; set; }
    public Models.Media? MediaToSave { get; set; }
    public Guid? ParentFolderId { get; set; }
    public bool IsUpdate { get; set; }
}