using System;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class ClearUnpublishedContentParameters
{
    public Guid ContentId { get; set; }
}