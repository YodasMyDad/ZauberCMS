namespace ZauberCMS.Core.Data.Interfaces;

public interface ISeedData
{
    void Initialise(ZauberDbContext dbContext);
}