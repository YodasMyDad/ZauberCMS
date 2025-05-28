using ZauberCMS.Core.Data;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Extensions;

namespace ZauberCMS.Core.SeedData;

public class SyncTabsToUseAlias : ISeedData
{
    public void Initialise(IZauberDbContext dbContext)
    {
        // Get all ContentTypes
        // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataQuery
        foreach (var contentType in dbContext.ContentTypes)
        {
            // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataUsage
            foreach (var contentProperty in contentType.ContentProperties)
            {
                if (contentProperty.TabAlias.IsNullOrWhiteSpace())
                {
                    // ReSharper disable once EntityFramework.NPlusOne.IncompleteDataUsage
                    var tab = contentType.Tabs.FirstOrDefault(x => x.Id == contentProperty.TabId);
                    if (tab != null)
                    {
                        contentProperty.TabAlias = tab.Alias;
                    }
                }
            }
        }
            
        dbContext.SaveChanges();
    }
}