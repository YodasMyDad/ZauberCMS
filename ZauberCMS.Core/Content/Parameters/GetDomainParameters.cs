using System;
using ZauberCMS.Core.Content.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class GetDomainParameters
{
    public Guid? Id { get; set; }
    public bool AsNoTracking { get; set; }
    public string? Url { get; set; }
}