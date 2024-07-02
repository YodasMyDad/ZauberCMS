namespace ZauberCMS.Core.Shared.Models;

public class TreeStub
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public string? Url { get; set; }
    public Type? StubType { get; set; }
    public List<TreeBranch> Branches { get; set; } = [];
}