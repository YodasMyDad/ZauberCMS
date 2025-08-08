using System;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Parameters;

public class DeleteLanguageParameters
{
    public Guid? Id { get; set; }
    public string? LanguageIsoCode { get; set; }
}