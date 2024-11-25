﻿using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Core.Languages.Models;

public class Language : ITreeItem
{
    /// <summary>
    /// The ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid().NewSequentialGuid();

    public string? Name
    {
        get => LanguageCultureName;
        set => LanguageCultureName = value;
    }

    /// <summary>
    /// Language ISO code
    /// </summary>
    public string? LanguageIsoCode { get; set; }
    
    /// <summary>
    /// Language culture name
    /// </summary>
    public string? LanguageCultureName { get; set; }
    
    /// <summary>
    /// The date and time when the item was created.
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The date and time the item was updated
    /// </summary>
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Domains using this language
    /// </summary>
    public List<Domain> Domains { get; set; } = [];
    
    /// <summary>
    /// Language Text using this language
    /// </summary>
    public List<LanguageText> LanguageTexts { get; set; } = [];
}