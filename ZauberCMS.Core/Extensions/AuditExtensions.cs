using ZauberCMS.Core.Audit.Interfaces;
using ZauberCMS.Core.Audit.Parameters;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Extensions;

public static class AuditExtensions
{
    public static async Task AddAudit<T>(this User? user, T entity, string? name, AuditAction action, IAuditService auditService, CancellationToken? cancellationToken) where T : class
    {
        if (user != null)
        {
            var nameText = name ?? entity.GetType().Name;
            var text = $"{user.Name} {ActionWording(action)} {nameText}";
            await auditService.SaveAuditAsync(new SaveAuditParameters
            {
                Audit = new Audit.Models.Audit
                {
                    Description = text
                }
            }, cancellationToken ?? CancellationToken.None);   
        }
    }

    private static string ActionWording(AuditAction action)
    {
        return action switch
        {
            AuditAction.Create => "Created",
            AuditAction.Delete => "Deleted",
            AuditAction.Update => "Updated",
            AuditAction.Move => "Moved",
            AuditAction.RecycleBin => "Recycle Binned",
            AuditAction.Misc => "Misc action on",
            AuditAction.Copy => "Copied",
            _ => string.Empty
        };
    }
    
    public enum AuditAction
    {
        Create,
        Delete,
        Update,
        Move,
        RecycleBin,
        Misc,
        Copy
    }
}