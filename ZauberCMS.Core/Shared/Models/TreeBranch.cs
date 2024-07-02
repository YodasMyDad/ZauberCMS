namespace ZauberCMS.Core.Shared.Models;

public class TreeBranch
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public List<TreeBranch> Branches { get; set; } = [];
    public Type? BranchType { get; set; }
}