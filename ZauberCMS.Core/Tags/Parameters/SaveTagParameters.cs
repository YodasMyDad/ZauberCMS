using System;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Parameters;

public class SaveTagParameters
{
    public Guid? Id { get; set; }
    public string? TagName { get; set; }
    public int SortOrder { get; set; }
}