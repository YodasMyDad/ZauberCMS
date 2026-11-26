using System;

namespace ZauberCMS.Core.Media.Parameters;

/// <summary>
/// Takes a media item out of the recycle bin, with the items that went to the bin with it.
/// </summary>
public class RestoreMediaParameters
{
    public Guid MediaId { get; set; }
}
