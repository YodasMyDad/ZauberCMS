using System;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Parameters;

public class DeleteTagItemParameters
{
    public Guid? TagId { get; set; }
    public Guid? ItemId { get; set; }
}