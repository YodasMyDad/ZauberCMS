namespace ZauberCMS.Core.Membership.Models;

public class UsersTreeStub
{
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public List<UsersTreeBranch> Branches { get; set; } = [];
}

public class UsersTreeBranch
{
    public string? Name { get; set; }
    public string? Icon { get; set; }
    public string? Url { get; set; }
    public List<UsersTreeBranch> Branches { get; set; } = [];
}