using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Shared.Models;

public class TreeBranch : ITreeItem
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public List<TreeBranch> Branches { get; set; } = [];
    public Type? BranchType { get; set; }
}