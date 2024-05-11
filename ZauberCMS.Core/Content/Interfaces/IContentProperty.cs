﻿using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Core.Content.Interfaces;

public interface IContentProperty
{
    string Name { get; set; }
    string Alias { get; set; }
    string Value { get; set; }
    string Icon { get; set; }
    EventCallback<string> ValueChanged { get; set; }
}