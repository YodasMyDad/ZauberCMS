using System;

namespace ZauberCMS.Core.Media.Parameters;

public class DeleteMediaParameters
{
    public Guid MediaId { get; set; }

    /// <summary>
    /// Permanent delete only: also remove the files from storage (default). A file that another media item still
    /// points at is kept. Ignored when the item goes to the recycle bin.
    /// </summary>
    public bool DeleteFile { get; set; } = true;

    /// <summary>
    /// True (default): the item and everything below it go to the recycle bin, where they can be restored.
    /// False: the item is deleted for good, with everything below it, which must already be in the recycle bin.
    /// </summary>
    public bool MoveToRecycleBin { get; set; } = true;
}
