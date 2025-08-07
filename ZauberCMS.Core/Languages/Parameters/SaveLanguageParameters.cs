using System.Globalization;

namespace ZauberCMS.Core.Languages.Parameters;

public class SaveLanguageParameters
{
    public CultureInfo? CultureInfo { get; set; }
    public Guid? Id { get; set; }
}