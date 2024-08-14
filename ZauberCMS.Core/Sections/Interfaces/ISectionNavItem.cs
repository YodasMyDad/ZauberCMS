using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNavItem : IDisposable
{
    string SectionNavAlias { get; }
    int SortOrder { get; }
    IEnumerable<TreeStub> Trees { get; set; }
    void OnChange(object? selection);
}