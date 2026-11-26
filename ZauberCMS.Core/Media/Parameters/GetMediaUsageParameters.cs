using System;
using System.Collections.Generic;

namespace ZauberCMS.Core.Media.Parameters;

/// <summary>
/// Finds the pages and members whose property values point at the given media items.
/// </summary>
public class GetMediaUsageParameters
{
    public List<Guid> MediaIds { get; set; } = [];

    /// <summary>Look at every item in the recycle bin as well as <see cref="MediaIds"/> (for emptying the bin).</summary>
    public bool AllInRecycleBin { get; set; }

    /// <summary>Also look for the items below each one (the contents of a folder).</summary>
    public bool IncludeDescendants { get; set; } = true;

    /// <summary>Stop after this many uses.</summary>
    public int Max { get; set; } = 100;
}
