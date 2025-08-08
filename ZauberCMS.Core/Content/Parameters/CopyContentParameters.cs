using System;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class CopyContentParameters
{
    public Guid ContentToCopy { get; set; }
    public bool IncludeDescendants { get; set; }
    public Guid? CopyTo { get; set; }
}