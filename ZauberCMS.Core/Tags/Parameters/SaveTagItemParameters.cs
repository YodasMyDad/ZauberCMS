using System;
using System.Collections.Generic;
using ZauberCMS.Core.Shared.Models;
using ZauberCMS.Core.Tags.Models;

namespace ZauberCMS.Core.Tags.Parameters;

public class SaveTagItemParameters
{
    public List<Guid> TagIds { get; set; } = [];
    public Guid ItemId { get; set; }
}