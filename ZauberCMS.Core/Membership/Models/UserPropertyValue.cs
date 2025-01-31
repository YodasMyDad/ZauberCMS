using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.Membership.Models;

public class UserPropertyValue : IPropertyValue
{
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();
    public string Alias { get; set; } = string.Empty;
    public Guid ContentTypePropertyId { get; set; }
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    public string Value { get; set; } = string.Empty;
    public DateTime? DateUpdated { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }    
    public User? User { get; set; }
}