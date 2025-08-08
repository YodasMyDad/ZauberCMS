using System.Collections.Generic;
using ZauberCMS.Core.Data.Interfaces;

namespace ZauberCMS.Core.Data.Parameters;

public class MultiQueryParameters
{
    public List<IQueryModel> Queries { get; set; } = [];
}