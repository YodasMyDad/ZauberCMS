namespace ZauberCMS.Core.Content.Interfaces;

public interface IPropertyValue
{
    public Guid Id { get; set; }
    public string Alias { get; set; }
    public string Value { get; set; }
    public DateTime? DateUpdated { get; set; }
}