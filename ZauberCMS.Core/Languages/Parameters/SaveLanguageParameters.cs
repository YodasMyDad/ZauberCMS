using System;
using System.Globalization;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Parameters;

public class SaveLanguageParameters
{
    public CultureInfo? CultureInfo { get; set; }
    public Guid? Id { get; set; }
}