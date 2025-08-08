using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ZauberCMS.Core.Languages.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Languages.Parameters;

public class QueryLanguageParameters
{
    public bool AsNoTracking { get; set; } = true;
    public List<Guid> Ids { get; set; } = [];
    public int PageIndex { get; set; } = 1;
    public int AmountPerPage { get; set; } = 10;
    public bool IncludeChildren { get; set; }
    public List<string> LanguageIsoCodes { get; set; } = [];
    public GetLanguageOrderBy OrderBy { get; set; } = GetLanguageOrderBy.DateCreatedDescending;
    public Expression<Func<Language, bool>>? WhereClause { get; set; }
    public Func<IQueryable<Language>>? Query { get; set; }
}

public enum GetLanguageOrderBy
{
    DateCreated,
    DateCreatedDescending,
    LanguageIsoCode,
    LanguageCultureName
}