namespace ZauberCMS.Core.Sections.Interfaces;

public interface ISectionNavGroup
{
    string Heading { get; }
    string Alias { get; }
    int SortOrder { get; }
    string SectionAlias { get; }
}