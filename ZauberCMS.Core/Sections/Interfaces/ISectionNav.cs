namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNav : IDisposable
{
    string Heading { get; }
    int SortOrder { get; }
    string SectionAlias { get; }
}