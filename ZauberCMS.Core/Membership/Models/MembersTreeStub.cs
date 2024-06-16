namespace ZauberCMS.Core.Membership.Models;

public class MembersTreeStub
{
    public string? Name { get; set; }
    public TreeStubType StubType { get; set; }
}

public enum TreeStubType
{
    Members,
    Roles
}