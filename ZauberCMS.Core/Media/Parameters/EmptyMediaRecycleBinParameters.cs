namespace ZauberCMS.Core.Media.Parameters;

/// <summary>
/// Permanently deletes every media item in the recycle bin.
/// </summary>
public class EmptyMediaRecycleBinParameters
{
    /// <summary>Also remove the files from storage (default). A file another media item still points at is kept.</summary>
    public bool DeleteFiles { get; set; } = true;
}
