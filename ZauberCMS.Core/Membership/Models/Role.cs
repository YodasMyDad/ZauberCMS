﻿using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using ZauberCMS.Core.Shared.Interfaces;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Models;

public class Role : IdentityRole<Guid>, ITreeItem
{
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> ExtendedData { get; set; } = new();
    public List<UserRole> UserRoles { get; set; } = [];
    
    [JsonIgnore]
    public List<ContentRole> ContentRoles { get; set; } = [];
    
    [JsonIgnore]
    public List<MediaRole> MediaRoles { get; set; } = [];
    
    /// <summary>
    /// The properties available on this Role
    /// </summary>
    public List<PropertyType> Properties { get; set; } = [];
    
    /// <summary>
    /// Gets or sets the list of tabs associated with this Role
    /// </summary>
    /// <remarks>
    /// Tabs are used to organize the properties into separate sections.
    /// Each tab represents a logical grouping of properties.
    /// </remarks>
    public List<Tab> Tabs { get; set; } = [new() {Id = Constants.Guids.ContentTypeSystemTabId, IsSystemTab = true, SortOrder = 100, Name = "System"}];
}