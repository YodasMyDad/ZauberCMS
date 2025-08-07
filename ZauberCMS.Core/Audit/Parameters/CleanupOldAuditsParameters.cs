namespace ZauberCMS.Core.Audit.Parameters;

public class CleanupOldAuditsParameters
{
    public int DaysToKeep { get; set; } = 90;
}