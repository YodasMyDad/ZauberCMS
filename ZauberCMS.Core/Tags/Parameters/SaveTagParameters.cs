namespace ZauberCMS.Core.Tags.Parameters;

public class SaveTagParameters
{
    public Guid? Id { get; set; }
    public string? TagName { get; set; }
    public int SortOrder { get; set; }
}