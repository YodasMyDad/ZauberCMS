using System;
using Microsoft.AspNetCore.Components.Forms;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Parameters;

public class SaveMediaParameters
{
    public IBrowserFile? FileToSave { get; set; }
    public Media.Models.Media? MediaToSave { get; set; }
    public Guid? ParentFolderId { get; set; }
    public bool IsUpdate { get; set; }
}