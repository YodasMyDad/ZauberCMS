using ZauberCMS.Core.Data.Interfaces;

namespace ZauberCMS.Core.Data.Parameters;

public class MultiQueryParameters
{
    public List<IQueryModel> Queries { get; set; } = [];
    
    public MultiQueryParameters()
    {
    }
    
    public MultiQueryParameters(List<IQueryModel> queries)
    {
        Queries = queries;
    }
}