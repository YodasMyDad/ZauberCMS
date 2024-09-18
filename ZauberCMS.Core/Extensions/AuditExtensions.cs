using MediatR;
using ZauberCMS.Core.Audit.Commands;
using ZauberCMS.Core.Membership.Models;

namespace ZauberCMS.Core.Extensions;

public static class AuditExtensions
{
    public static async Task AddAudit<T>(this User? user, T entity, string? name, AuditAction action, IMediator mediator, CancellationToken? cancellationToken) where T : class
    {
        if (user != null)
        {
            var nameText = name ?? entity.GetType().Name;
            var text = $"{user.Name} {ActionWording(action)} {nameText}";
            await mediator.Send(new SaveAuditCommand
            {
                Audit = new Audit.Models.Audit
                {
                    Description = text
                }
            });   
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
        Misc
    }
}