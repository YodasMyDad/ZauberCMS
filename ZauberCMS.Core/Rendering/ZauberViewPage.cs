﻿namespace ZauberCMS.Core.Rendering;

public abstract class ZauberViewPage<T> : Microsoft.AspNetCore.Mvc.Razor.RazorPage<T>, IZauberViewPage
{
    //public Content.Models.Content? CurrentPage => TempData["CurrentPage"] as Content.Models.Content;
   public Dictionary<string, string> LanguageKeys => TempData["LanguageKeys"] as Dictionary<string, string> ?? new Dictionary<string, string>();
}