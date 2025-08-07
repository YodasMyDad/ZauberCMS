namespace ZauberCMS.Core.Tags.Parameters;

public class SaveTagItemParameters
{
    public List<Guid> TagIds { get; set; } = [];
    public Guid ItemId { get; set; }
}