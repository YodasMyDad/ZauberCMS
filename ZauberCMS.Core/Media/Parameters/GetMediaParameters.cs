using System;
using ZauberCMS.Core.Media.Models;

namespace ZauberCMS.Core.Media.Parameters;

public class GetMediaParameters
{
    public Guid? Id { get; set; }
    public bool IncludeChildren { get; set; }
    public bool IncludeParent { get; set; }
    public bool Cached { get; set; }
    public bool AsNoTracking { get; set; } = true;
    public MediaType? MediaType { get; set; }
}