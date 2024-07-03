using System.Text.Json.Serialization;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Content.Models;

public class ContentPropertyValue
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public Guid ContentId { get; set; }
    [JsonIgnore]
    public Content? Content { get; set; }
    public string Alias { get; set; } = string.Empty;
    public Guid ContentTypePropertyId { get; set; }
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    public string Value { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
}