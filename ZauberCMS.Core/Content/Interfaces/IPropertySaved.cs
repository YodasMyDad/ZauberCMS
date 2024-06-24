namespace ZauberCMS.Core.Content.Interfaces;

public interface IPropertySaved
{
    string PropertyAlias { get; set; }
    string Update(string propertyValue);
}