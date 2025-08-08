using System;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Parameters;

public class DeleteContentParameters
{
    public Guid ContentId { get; set; }
    public bool MoveToRecycleBin { get; set; }
}