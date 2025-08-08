using System;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Parameters;

public class GetLanguageParameters
{
    public Guid? Id { get; set; }
    public bool AsNoTracking { get; set; }
    public string? LanguageIsoCode { get; set; }
}