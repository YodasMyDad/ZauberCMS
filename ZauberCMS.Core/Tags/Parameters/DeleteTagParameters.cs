using System;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Parameters;

public class DeleteTagParameters
{
    public Guid? Id { get; set; }
    public string? TagName { get; set; }
}