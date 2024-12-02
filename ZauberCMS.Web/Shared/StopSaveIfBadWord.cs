using Microsoft.EntityFrameworkCore;
using ZauberCMS.Core.Content.Models;
using ZauberCMS.Core.Shared.Interfaces;

namespace ZauberCMS.Web.Shared;

public class StopSaveIfBadWord : IBeforeEntitySave
{
    public Type EntityType => typeof(Content);
    public bool BeforeSave<T>(T entity, EntityState entityState)
    {
        if (entity is Content content)
        {
            if (content.Name != null && content.Name.Contains("Arsenal"))
            {
                content.Name = content.Name.Replace("Arsenal", "Up The Spurs #COYS");
            }
        }

        return true;
    }
}